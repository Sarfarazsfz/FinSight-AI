using FinSight.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.ErrorHandling;

public sealed class GlobalExceptionHandler
    : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler>
        _logger;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        var statusCode =
            exception switch
            {
                AiProviderUnavailableException =>
                    StatusCodes.Status503ServiceUnavailable,

                FinanceAssistantProviderUnavailableException =>
                    StatusCodes.Status503ServiceUnavailable,

                ArgumentException =>
                    StatusCodes.Status400BadRequest,

                InvalidDataException =>
                    StatusCodes.Status400BadRequest,

                KeyNotFoundException =>
                    StatusCodes.Status404NotFound,

                UnauthorizedAccessException =>
                    StatusCodes.Status403Forbidden,

                _ =>
                    StatusCodes.Status500InternalServerError
            };

        var title =
            statusCode switch
            {
                StatusCodes.Status400BadRequest =>
                    "Bad Request",

                StatusCodes.Status403Forbidden =>
                    "Forbidden",

                StatusCodes.Status404NotFound =>
                    "Resource Not Found",

                StatusCodes.Status503ServiceUnavailable =>
                    "AI Provider Unavailable",

                _ =>
                    "An unexpected error occurred."
            };

        if (statusCode >=
            StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception while processing {Method} {Path}.",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning(
                exception,
                "Request failed with status {StatusCode} for {Method} {Path}.",
                statusCode,
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        var problemDetails =
            new ProblemDetails
            {
                Status = statusCode,

                Title = title,

                Type =
                    $"https://httpstatuses.com/{statusCode}",

                Instance =
                    httpContext.Request.Path
            };

        // Scoped to this one exception type only -- every other type
        // (including AiProviderUnavailableException/F9) keeps its existing
        // detail-less ProblemDetails shape unchanged.
        if (exception is FinanceAssistantProviderUnavailableException)
        {
            problemDetails.Detail =
                "Finance Assistant temporarily unavailable. " +
                "Reconciliation results are unaffected.";
        }

        problemDetails.Extensions["traceId"] =
            httpContext.TraceIdentifier;

        // Explicitly set the HTTP response status code.
        httpContext.Response.StatusCode =
            statusCode;

        await httpContext.RequestServices
            .GetRequiredService<IProblemDetailsService>()
            .WriteAsync(
                new ProblemDetailsContext
                {
                    HttpContext =
                        httpContext,

                    ProblemDetails =
                        problemDetails
                });

        return true;
    }
}