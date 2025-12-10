using HFiles_Backend.Application.Common;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Authentication;

namespace HFiles_Main.API.Middlewares
{
    /// <summary>
    /// Middleware that globally intercepts unhandled exceptions during HTTP request processing.
    /// Captures, logs, and translates exceptions into structured API responses with appropriate HTTP status codes.
    /// Ensures consistent error handling across the application with enhanced traceability for diagnostics and security auditing.
    /// </summary>
    /// <remarks>
    /// Initializes the middleware with the required components.
    /// </remarks>
    /// <param name="next">The next middleware to invoke.</param>
    /// <param name="logger">Logger to record exception details.</param>
    public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        /// <summary>
        /// Represents the next delegate in the middleware pipeline.
        /// </summary>
        private readonly RequestDelegate _next = next;

        /// <summary>
        /// Logger instance specific to the GlobalExceptionMiddleware.
        /// Used to record warnings and errors with contextual information.
        /// </summary>
        private readonly ILogger<GlobalExceptionMiddleware> _logger = logger;

        /// <summary>
        /// Invokes the middleware logic within the HTTP pipeline.
        /// Wraps downstream calls in exception handling blocks and returns standardized error responses.
        /// </summary>
        /// <param name="context">HTTP context for the incoming request.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Proceed with the next middleware or endpoint.
                await _next(context);
            }
            catch (UnauthorizedAccessException ex)
            {
                // Log unauthorized access attempts and return a 401 response.
                _logger.LogWarning(ex, "Unauthorized access attempt.");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(ApiResponseFactory.Fail("Unauthorized access."));
            }
            catch (AuthenticationException ex)
            {
                // Log authentication failures and return a 403 Forbidden response.
                _logger.LogWarning(ex, "Authentication failed.");
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(ApiResponseFactory.Fail("Authentication error."));
            }
            catch (ValidationException ex)
            {
                // Log input validation issues and return a 400 Bad Request response with details.
                _logger.LogWarning(ex, "Validation failed.");
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(ApiResponseFactory.Fail("Validation error.", ex.Message));
            }
            catch (ArgumentException ex)
            {
                // Log argument-related errors and return a 400 Bad Request response.
                _logger.LogWarning(ex, "Invalid argument provided.");
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(ApiResponseFactory.Fail("Invalid argument.", ex.Message));
            }
            catch (OperationCanceledException ex)
            {
                // Log cancellation or timeout events and return a 408 Request Timeout response.
                _logger.LogWarning(ex, "Request was cancelled.");
                context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
                await context.Response.WriteAsJsonAsync(ApiResponseFactory.Fail("Request was cancelled or timed out."));
            }
            catch (HttpRequestException ex)
            {
                // Log downstream service errors and return a 502 Bad Gateway response.
                _logger.LogError(ex, "HTTP request failed.");
                context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
                await context.Response.WriteAsJsonAsync(ApiResponseFactory.Fail("Downstream service error."));
            }
            catch (Exception ex)
            {
                // Catch-all for unexpected exceptions; logs and returns a generic 500 error.
                _logger.LogError(ex, "Unhandled exception occurred.");
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(ApiResponseFactory.Fail("An unexpected error occurred."));
            }
        }
    }
}
