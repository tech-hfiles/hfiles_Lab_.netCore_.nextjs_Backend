using HFiles_Backend.API.Middleware;

namespace HFiles_Backend.API.Extensions
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseJwtBlacklistMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<JwtBlacklistMiddleware>();
        }
    }
}
