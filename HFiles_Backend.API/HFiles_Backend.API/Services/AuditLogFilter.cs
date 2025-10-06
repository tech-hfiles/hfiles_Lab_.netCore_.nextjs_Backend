using System.Security.Claims;
using System.Text.Json;
using HFiles_Backend.Domain.Entities.Labs;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HFiles_Backend.API.Services
{
    public class AuditLogFilter(IHttpContextAccessor httpContextAccessor, ILogger<AuditLogFilter> logger, AppDbContext context) : IAsyncActionFilter
    {
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
        private readonly ILogger<AuditLogFilter> _logger = logger;
        private readonly AppDbContext _context = context;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var httpContext = _httpContextAccessor.HttpContext;

            if (httpContext?.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) == true)
            {
                await next();
                return;
            }

            var resultContext = await next();

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
                        if(labAdminId != 0)
                        {
                            userId = labAdminId;
                        }

                        // If not found, try ClinicAdminId
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

                        if (dataElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in dataElement.EnumerateArray())
                            {
                                if (item.ValueKind != JsonValueKind.Object) continue;

                                int? responseLabId = item.TryGetProperty("branchLabId", out var branchIdProp) &&
                                                     branchIdProp.ValueKind == JsonValueKind.Number &&
                                                     branchIdProp.TryGetInt32(out var branchIdVal)
                                    ? branchIdVal
                                    : null;

                                string? notificationMessage = item.TryGetProperty("notificationMessage", out var note) &&
                                                              note.ValueKind == JsonValueKind.String
                                    ? note.GetString()
                                    : "No notification message found.";

                                var log = new LabAuditLog
                                {
                                    LabId = labId,
                                    UserId = userId,
                                    UserRole = userRole,
                                    BranchId = responseLabId != labId ? responseLabId : null,
                                    EntityName = context.ActionDescriptor.RouteValues["controller"],
                                    Category = httpContext.Items["Log-Category"]?.ToString(),
                                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                    IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
                                    SessionId = sessionId,
                                    Url = httpContext.Request.Path,
                                    HttpMethod = httpContext.Request.Method,
                                    Details = $"""
                                        Response Item: {TruncateIfNeeded(item.ToString())}
                                        """,
                                    Notifications = notificationMessage
                                };

                                dbContext.LabAuditLogs.Add(log);
                            }

                            await dbContext.SaveChangesAsync();
                        }

                        else if (dataElement.ValueKind == JsonValueKind.Object)
                        {
                            int? responseLabId = dataElement.TryGetProperty("branchLabId", out var branchIdProp) &&
                                                 branchIdProp.ValueKind == JsonValueKind.Number &&
                                                 branchIdProp.TryGetInt32(out var branchIdVal)
                                ? branchIdVal
                                : null;

                            string? notificationMessage = dataElement.TryGetProperty("notificationMessage", out var note) &&
                                                          note.ValueKind == JsonValueKind.String
                                ? note.GetString()
                                : "No notification message found.";

                            string? userNotificationMessage = dataElement.TryGetProperty("userNotificationMessage", out var usernote) &&
                                                         usernote.ValueKind == JsonValueKind.String
                               ? usernote.GetString()
                               : "No user notification message found.";

                            var log = new LabAuditLog
                            {
                                LabId = labId,
                                UserId = userId,
                                UserRole = userRole,
                                BranchId = responseLabId != labId ? responseLabId : null,
                                EntityName = context.ActionDescriptor.RouteValues["controller"],
                                Category = httpContext.Items["Log-Category"]?.ToString(),
                                SentToUserId = httpContext.Items["Sent-To-UserId"] as int?,
                                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
                                SessionId = sessionId,
                                Url = httpContext.Request.Path,
                                HttpMethod = httpContext.Request.Method,
                                Details = $"""
                                    Response Body: {TruncateIfNeeded(responsePayload)}
                                    """,
                                Notifications = notificationMessage,
                                SentToUserNotifications = userNotificationMessage
                            };

                            dbContext.LabAuditLogs.Add(log);
                            await dbContext.SaveChangesAsync();
                        }

                        if (dataElement.ValueKind == JsonValueKind.Object)
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
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save audit log with request/response payload.");
                }
            }
        }

        private static string TruncateIfNeeded(string input, int maxLength = 3000)
        {
            if (string.IsNullOrWhiteSpace(input)) return "Empty";
            return input.Length <= maxLength ? input : input[..maxLength] + " ...[truncated]";
        }
    }
}
