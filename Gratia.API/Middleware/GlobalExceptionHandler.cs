using Gratia.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Gratia.API.Middleware
{
    /// <summary>
    /// Turns every exception thrown while handling a request into a ProblemDetails response,
    /// so controllers don't need try/catch blocks.
    /// </summary>
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly IProblemDetailsService _problemDetailsService;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(IProblemDetailsService problemDetailsService, IHostEnvironment environment, ILogger<GlobalExceptionHandler> logger)
        {
            _problemDetailsService = problemDetailsService;
            _environment = environment;
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var (statusCode, title) = exception switch
            {
                NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
                ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
                ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
                BusinessRuleException => (StatusCodes.Status400BadRequest, "Bad request"),
                EmailDeliveryException => (StatusCodes.Status503ServiceUnavailable, "Email unavailable"),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
            };

            // Expected errors carry a message meant for the client; unexpected ones are logged and kept
            // private outside development
            var isUnexpected = statusCode == StatusCodes.Status500InternalServerError;
            if (isUnexpected)
            {
                _logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                    httpContext.Request.Method, httpContext.Request.Path);
            }
            else if (exception is EmailDeliveryException)
            {
                // The client gets a plain message; the SMTP error is in the inner exception, for whoever runs the API
                _logger.LogError(exception, "Couldn't send an email while processing {Method} {Path}",
                    httpContext.Request.Method, httpContext.Request.Path);
            }

            httpContext.Response.StatusCode = statusCode;
            return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = title,
                    Detail = !isUnexpected || _environment.IsDevelopment() ? exception.Message : null
                }
            });
        }
    }
}
