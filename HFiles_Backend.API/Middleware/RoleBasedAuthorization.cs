using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Authorization;
using HFiles_Backend.Application.Common;
using Newtonsoft.Json;

namespace HFiles_Backend.API.Middleware
{
    public class RoleBasedAuthorization : IAuthorizationMiddlewareResultHandler
    {
        private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

        public async Task HandleAsync(
            RequestDelegate next,
            HttpContext context,
            AuthorizationPolicy policy,
            PolicyAuthorizationResult authorizeResult)
        {
            if (authorizeResult.Forbidden)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                var result = ApiResponseFactory.Fail("Access Denied: You do not have permission to access this resource.");
                await context.Response.WriteAsync(JsonConvert.SerializeObject(result));
                return;
            }

            if (authorizeResult.Challenged)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                var result = ApiResponseFactory.Fail("Authentication required. Please provide a valid token.");
                await context.Response.WriteAsync(JsonConvert.SerializeObject(result));
                return;
            }

            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
        }
    }

}
