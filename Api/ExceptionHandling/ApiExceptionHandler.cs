using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace BuildingCopilot.Api.ExceptionHandling;

public sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        var (status, title, detail) = exception switch
        {
            BrokenCircuitException => (
                StatusCodes.Status503ServiceUnavailable,
                "Language model service temporarily unavailable",
                "Requests are paused while the configured language model service recovers."),
            TimeoutRejectedException => (
                StatusCodes.Status504GatewayTimeout,
                "Language model request timed out",
                "The configured language model service did not respond within the allowed time."),
            HttpRequestException { StatusCode: null } => (
                StatusCodes.Status503ServiceUnavailable,
                "Language model service unavailable",
                "The configured language model service could not be reached."),
            HttpRequestException => (
                StatusCodes.Status502BadGateway,
                "Language model request failed",
                "The configured language model service returned an unsuccessful response."),
            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred",
                "The request could not be completed.")
        };

        logger.LogError(exception, "Request failed with status code {StatusCode}.", status);
        httpContext.Response.StatusCode = status;

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });

        if (!written && !httpContext.Response.HasStarted)
        {
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        }

        return true;
    }
}