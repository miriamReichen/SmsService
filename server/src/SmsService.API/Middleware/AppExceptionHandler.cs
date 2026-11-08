using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SmsService.Application;

namespace SmsService.API.Middleware;

/// <summary>Maps <see cref="AppException"/> to RFC 7807 ProblemDetails with an extra "code" field.</summary>
public class AppExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<AppExceptionHandler> _logger;

    public AppExceptionHandler(IProblemDetailsService problemDetails, ILogger<AppExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        if (exception is not AppException appException)
        {
            _logger.LogError(exception, "Unhandled exception");
            return false; // default 500 ProblemDetails
        }

        httpContext.Response.StatusCode = appException.StatusCode;
        var problem = new ProblemDetails
        {
            Status = appException.StatusCode,
            Title = appException.StatusCode == 404 ? "Not found" : "Validation failed",
            Detail = appException.Message,
        };
        problem.Extensions["code"] = appException.Code;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
