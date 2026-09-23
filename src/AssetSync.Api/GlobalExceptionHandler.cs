using AssetSync.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AssetSync.Api;

/// <summary>
/// Single place that turns exceptions into ProblemDetails responses.
/// FluentValidation failures become a 400 with per-field messages,
/// NotFoundException becomes a 404, and anything else is logged and
/// returned as a generic 500 — no internal details leak to the client.
/// </summary>
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case ValidationException validationException:
                var errors = validationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await httpContext.Response.WriteAsJsonAsync(
                    new HttpValidationProblemDetails(errors), cancellationToken);
                return true;

            case NotFoundException notFoundException:
                httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = notFoundException.Message,
                }, cancellationToken);
                return true;

            default:
                logger.LogError(exception, "Unhandled exception.");
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred.",
                }, cancellationToken);
                return true;
        }
    }
}
