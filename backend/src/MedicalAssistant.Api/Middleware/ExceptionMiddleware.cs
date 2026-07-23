using MedicalAssistant.Api.Models;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Exceptions;
using System.Net;
using System.Security.Claims;

namespace MedicalAssistant.Api.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(httpContext, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext httpContext, Exception ex)
    {
        HttpStatusCode statusCode = HttpStatusCode.InternalServerError;
        CustomProblemDetails problem;

        var user = httpContext.User;
        var userId = user?.FindFirst("uid")?.Value ?? "Anonymous";
        var userName = user?.FindFirst(ClaimTypes.Name)?.Value ?? user?.FindFirst("sub")?.Value;
        var traceId = httpContext.TraceIdentifier;
        var method = httpContext.Request.Method;
        var path = httpContext.Request.Path;

        switch (ex)
        {
            case BadRequestException badRequestException:
                statusCode = HttpStatusCode.BadRequest;
                problem = new CustomProblemDetails
                {
                    Title = badRequestException.Message,
                    Status = (int)statusCode,
                    Type = nameof(BadRequestException),
                    Errors = badRequestException.ValidationErrors,
                    Instance = httpContext.Request.Path
                };
                break;
            case NotFoundException notFoundException:
                statusCode = HttpStatusCode.NotFound;
                problem = new CustomProblemDetails
                {
                    Title = notFoundException.Message,
                    Status = (int)statusCode,
                    Type = nameof(NotFoundException),
                    Instance = httpContext.Request.Path
                };
                break;
            default:
                problem = new CustomProblemDetails
                {
                    Title = "An unexpected error occurred. Please try again later.",
                    Status = (int)statusCode,
                    Type = "InternalServerError",
                    Instance = httpContext.Request.Path
                };
                break;
        }

        var isExpectedClientError = ex is BadRequestException or NotFoundException;

        if (!isExpectedClientError)
        {
            try
            {
                var errorLogger = httpContext.RequestServices.GetService<IErrorLogger>();
                if (errorLogger != null)
                    await errorLogger.LogAsync(ex, path, method);
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx,
                    "MedicalAssistant.Api Failed to write to ErrorLog TraceId {TraceId} UserId {UserId} Method {Method} Path {Path}",
                    traceId, userId, method, path);
            }
        }

        if (isExpectedClientError)
        {
            _logger.LogWarning(ex,
                "MedicalAssistant.Api Client error {ExceptionType} StatusCode {StatusCode} UserId {UserId} Method {Method} Path {Path} TraceId {TraceId}",
                ex.GetType().Name, (int)statusCode, userId, method, path, traceId);
        }
        else
        {
            _logger.LogError(ex,
                "MedicalAssistant.Api Unhandled exception {ExceptionType} StatusCode {StatusCode} UserId {UserId} Method {Method} Path {Path} TraceId {TraceId}",
                ex.GetType().Name, (int)statusCode, userId, method, path, traceId);
        }

        httpContext.Response.StatusCode = (int)statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem);
    }
}
