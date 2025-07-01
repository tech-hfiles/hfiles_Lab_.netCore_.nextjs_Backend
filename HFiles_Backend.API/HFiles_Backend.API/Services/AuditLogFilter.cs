using HFiles_Backend.Domain.Entities.Labs;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using System.Security.Claims;
using HFiles_Backend.Application.Common;
using Microsoft.EntityFrameworkCore;

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
                    var responsePayload = objectResult.Value != null
                        ? JsonSerializer.Serialize(objectResult.Value)
                        : "No response content";

                    var user = httpContext.User;
                    int? labId = user.FindFirst("UserId")?.Value is string labStr && int.TryParse(labStr, out var parsedLabId) ? parsedLabId : null;

                    var jsonDoc = JsonDocument.Parse(responsePayload);
                    var root = jsonDoc.RootElement;

                    int? responseLabId = root.TryGetProperty("data", out var dataElement) &&
                                         dataElement.TryGetProperty("BranchLabId", out var branchIdProp) &&
                                         branchIdProp.ValueKind == JsonValueKind.Number &&
                                         branchIdProp.TryGetInt32(out var branchIdVal)
                        ? branchIdVal
                        : null;

                    int? branchId = responseLabId != null && responseLabId != labId ? responseLabId : null;


                    var path = httpContext.Request.Path.ToString().ToLower();
                    var dtoArg = context.ActionArguments.Values.FirstOrDefault();

                    var log = new LabAuditLog
                    {
                        LabId = labId,
                        UserId = user.FindFirst("LabAdminId")?.Value is string userStr && int.TryParse(userStr, out var userId) ? userId : null,
                        UserRole = user.FindFirst(ClaimTypes.Role)?.Value,
                        BranchId = branchId,
                        EntityName = context.ActionDescriptor.RouteValues["controller"],
                        Category = httpContext.Items["Log-Category"]?.ToString(),
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
                        SessionId = user.FindFirst("SessionId")?.Value,
                        Url = httpContext.Request.Path,
                        HttpMethod = httpContext.Request.Method,
                        Details = $"""
                            Response Body: {TruncateIfNeeded(responsePayload)}
                            """,
                        Notifications = NotificationMessageRegistry.GenerateMessage(path, dtoArg),
                    };

                    var dbContext = httpContext.RequestServices.GetRequiredService<AppDbContext>();
                    dbContext.LabAuditLogs.Add(log);
                    await dbContext.SaveChangesAsync();

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to save audit log with request/response payload.");
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
