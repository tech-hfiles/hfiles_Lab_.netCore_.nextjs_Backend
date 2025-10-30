// HFiles_Backend/API/Services/AuditLogFilter.cs
using HFiles_Backend.Domain.Entities.Labs;
using HFiles_Backend.Domain.Entities.Users;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace HFiles_Backend.API.Services
{
    /// <summary>
    /// Audit log filter that creates both LabAuditLog and UserNotification
    /// in a single transaction for all successful non-GET requests
    /// </summary>
    public class AuditLogFilter : IAsyncActionFilter
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditLogFilter> _logger;
        private readonly AppDbContext _context;

        public AuditLogFilter(
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuditLogFilter> logger,
            AppDbContext context)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _context = context;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var httpContext = _httpContextAccessor.HttpContext;

            // Skip GET requests
            if (httpContext?.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) == true)
            {
                await next();
                return;
            }

            var resultContext = await next();

            // Only log successful responses (2xx status codes)
            if (resultContext.Result is ObjectResult objectResult &&
                objectResult.StatusCode is >= 200 and < 300)
            {
                try
                {
                    var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    var responsePayload = objectResult.Value != null
                        ? JsonSerializer.Serialize(objectResult.Value, jsonOptions)
                        : "No response content";

                    var user = httpContext!.User;
                    int? labId = user.FindFirst("UserId")?.Value is string labStr && int.TryParse(labStr, out var parsedLabId) ? parsedLabId : null;
                    int? userId = null;

                    // Try LabAdminId first
                    if (user.FindFirst("LabAdminId")?.Value is string labString && int.TryParse(labString, out var labAdminId))
                    {
                        if (labAdminId != 0)
                        {
                            userId = labAdminId;
                        }
                        else if (user.FindFirst("ClinicAdminId")?.Value is string clinicStr && int.TryParse(clinicStr, out var clinicAdminId))
                        {
                            userId = clinicAdminId;
                        }
                    }

                    string? userRole = user.FindFirst(ClaimTypes.Role)?.Value;
                    string? sessionId = user.FindFirst("SessionId")?.Value;

                    var jsonDoc = JsonDocument.Parse(responsePayload);
                    var root = jsonDoc.RootElement;

                    if (root.TryGetProperty("data", out var dataElement))
                    {
                        var dbContext = httpContext.RequestServices.GetRequiredService<AppDbContext>();

                        // Handle array responses
                        if (dataElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in dataElement.EnumerateArray())
                            {
                                if (item.ValueKind != JsonValueKind.Object) continue;

                                await ProcessNotificationItemAsync(
                                    item,
                                    dbContext,
                                    context,
                                    httpContext,
                                    labId,
                                    userId,
                                    userRole,
                                    sessionId,
                                    responsePayload,
                                    isArrayItem: true);
                            }

                            await dbContext.SaveChangesAsync();
                        }
                        // Handle single object responses
                        else if (dataElement.ValueKind == JsonValueKind.Object)
                        {
                            await ProcessNotificationItemAsync(
                                dataElement,
                                dbContext,
                                context,
                                httpContext,
                                labId,
                                userId,
                                userRole,
                                sessionId,
                                responsePayload,
                                isArrayItem: false);

                            await dbContext.SaveChangesAsync();

                            // Handle special case: array of notifications with array of branches
                            await ProcessResendNotificationsAsync(
                                dataElement,
                                dbContext,
                                context,
                                httpContext,
                                labId,
                                userId,
                                userRole,
                                sessionId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save audit log with request/response payload.");
                }
            }
        }

        /// <summary>
        /// Process a single notification item and create BOTH LabAuditLog AND UserNotification
        /// </summary>
        private async Task ProcessNotificationItemAsync(
            JsonElement item,
            AppDbContext dbContext,
            ActionExecutingContext context,
            HttpContext httpContext,
            int? labId,
            int? userId,
            string? userRole,
            string? sessionId,
            string responsePayload,
            bool isArrayItem)
        {
            int? responseLabId = item.TryGetProperty("branchLabId", out var branchIdProp) &&
                                 branchIdProp.ValueKind == JsonValueKind.Number &&
                                 branchIdProp.TryGetInt32(out var branchIdVal)
                ? branchIdVal
                : null;

            string? notificationMessage = item.TryGetProperty("notificationMessage", out var note) &&
                                          note.ValueKind == JsonValueKind.String
                ? note.GetString()
                : "No notification message found.";

            string? userNotificationMessage = item.TryGetProperty("userNotificationMessage", out var usernote) &&
                                             usernote.ValueKind == JsonValueKind.String
                ? usernote.GetString()
                : notificationMessage; // Fallback to notificationMessage

            // Get SentToUserId from HttpContext items (set by controller)
            int? sentToUserId = httpContext.Items["Sent-To-UserId"] as int?;

            // Get priority from response (optional)
            int priority = item.TryGetProperty("priority", out var priorityProp) &&
                          priorityProp.ValueKind == JsonValueKind.Number &&
                          priorityProp.TryGetInt32(out var priorityVal)
                ? priorityVal
                : 2; // Default to Normal

            var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // ✅ STEP 1: Create LabAuditLog
            var log = new LabAuditLog
            {
                LabId = labId,
                UserId = userId,
                UserRole = userRole,
                BranchId = responseLabId != labId ? responseLabId : null,
                EntityName = context.ActionDescriptor.RouteValues["controller"],
                Category = httpContext.Items["Log-Category"]?.ToString(),
                SentToUserId = sentToUserId,
                Timestamp = currentTimestamp,
                IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
                SessionId = sessionId,
                Url = httpContext.Request.Path,
                HttpMethod = httpContext.Request.Method,
                Details = isArrayItem
                    ? $"Response Item: {TruncateIfNeeded(item.ToString())}"
                    : $"Response Body: {TruncateIfNeeded(responsePayload)}",
                Notifications = notificationMessage,
                SentToUserNotifications = userNotificationMessage
            };

            dbContext.LabAuditLogs.Add(log);
            await dbContext.SaveChangesAsync(); // Save to get log.Id

            _logger.LogInformation(
                "✅ LabAuditLog created: Id={LogId}, Category={Category}, SentToUserId={SentToUserId}",
                log.Id, log.Category, sentToUserId);

            // ✅ STEP 2: Create UserNotification if SentToUserId is specified
            if (sentToUserId.HasValue && sentToUserId.Value > 0)
            {
                try
                {
                    // Check if UserNotification already exists (prevent duplicates)
                    var existingNotification = await dbContext.UserNotifications
                        .FirstOrDefaultAsync(un => un.AuditLogId == log.Id && un.UserId == sentToUserId.Value);

                    if (existingNotification != null)
                    {
                        _logger.LogInformation(
                            "UserNotification already exists for AuditLogId={AuditLogId}, UserId={UserId}. Skipping.",
                            log.Id, sentToUserId.Value);
                        return;
                    }

                    // Create new UserNotification
                    var userNotification = new UserNotification
                    {
                        UserId = sentToUserId.Value,
                        AuditLogId = log.Id,
                        IsRead = false,
                        IsDismissed = false,
                        Priority = priority,
                        CreatedAt = currentTimestamp, // Use same timestamp as audit log
                        ReadAt = null,
                        DismissedAt = null
                    };

                    dbContext.UserNotifications.Add(userNotification);
                    await dbContext.SaveChangesAsync(); // Commit in same transaction

                    _logger.LogInformation(
                        "✅ UserNotification created: Id={NotificationId}, AuditLogId={AuditLogId}, UserId={UserId}, Priority={Priority}",
                        userNotification.Id, log.Id, sentToUserId.Value, priority);

                    // ✅ OPTIONAL: Trigger SignalR notification via HTTP call to User Backend
                    // This can be done asynchronously without blocking the response
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await TriggerRealtimeNotificationAsync(userNotification.Id, sentToUserId.Value);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to trigger real-time notification");
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "❌ Failed to create UserNotification for AuditLogId={AuditLogId}, UserId={UserId}",
                        log.Id, sentToUserId.Value);
                    // Don't throw - audit log is still created
                }
            }
            else
            {
                _logger.LogInformation(
                    "No SentToUserId specified. UserNotification not created for AuditLogId={LogId}",
                    log.Id);
            }
        }

        /// <summary>
        /// Handle special case: resend notifications with arrays of messages and branches
        /// </summary>
        private async Task ProcessResendNotificationsAsync(
            JsonElement dataElement,
            AppDbContext dbContext,
            ActionExecutingContext context,
            HttpContext httpContext,
            int? labId,
            int? userId,
            string? userRole,
            string? sessionId)
        {
            if (dataElement.TryGetProperty("notificationMessage", out var notifArray) &&
                dataElement.TryGetProperty("labBranchId", out var branchArray) &&
                notifArray.ValueKind == JsonValueKind.Array &&
                branchArray.ValueKind == JsonValueKind.Array)
            {
                int count = Math.Min(notifArray.GetArrayLength(), branchArray.GetArrayLength());

                for (int i = 0; i < count; i++)
                {
                    string? notification = notifArray[i].ValueKind == JsonValueKind.String
                        ? notifArray[i].GetString()
                        : "No notification message found.";

                    int? branchId = branchArray[i].ValueKind == JsonValueKind.Number &&
                                    branchArray[i].TryGetInt32(out var bid)
                        ? bid
                        : null;

                    var resendLog = new LabAuditLog
                    {
                        LabId = labId,
                        UserId = userId,
                        UserRole = userRole,
                        BranchId = branchId != labId ? branchId : null,
                        EntityName = context.ActionDescriptor.RouteValues["controller"],
                        Category = httpContext.Items["Log-Category"]?.ToString(),
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
                        SessionId = sessionId,
                        Url = httpContext.Request.Path,
                        HttpMethod = httpContext.Request.Method,
                        Details = $"Resend Notification {i + 1}",
                        Notifications = notification
                    };

                    dbContext.LabAuditLogs.Add(resendLog);
                }

                await dbContext.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Trigger real-time notification via HTTP call to User Backend SignalR
        /// This is optional but provides immediate push notification
        /// </summary>
        private async Task TriggerRealtimeNotificationAsync(int notificationId, int userId)
        {
            try
            {
                // Read from environment variable
                var userBackendUrl = Environment.GetEnvironmentVariable("USER_BACKEND_URL")
                                  ?? "https://localhost:7113";
                var apiKey = Environment.GetEnvironmentVariable("INTERNAL_API_KEY");

                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);

                // Add API Key to request header
                if (!string.IsNullOrEmpty(apiKey))
                {
                    httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
                }

                var response = await httpClient.PostAsync(
                    $"{userBackendUrl}/api/internal/notifications/{notificationId}/trigger",
                    null);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "Real-time notification triggered. NotificationId={NotificationId}, UserId={UserId}",
                        notificationId, userId);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to trigger notification. Status={StatusCode}, NotificationId={NotificationId}",
                        response.StatusCode, notificationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error triggering notification. NotificationId={NotificationId}, UserId={UserId}",
                    notificationId, userId);
            }
        }

        private static string TruncateIfNeeded(string input, int maxLength = 3000)
        {
            if (string.IsNullOrWhiteSpace(input)) return "Empty";
            return input.Length <= maxLength ? input : input[..maxLength] + " ...[truncated]";
        }
    }
}