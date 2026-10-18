using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TourismHcm.Domain.Exceptions;

namespace TourismHcm.Api.Middleware;

/// <summary>
/// Translates unhandled exceptions-primarily the <see cref="DomainException"/> hierarchy raised
/// by the Application layer-into RFC 7807 <see cref="ProblemDetails"/> responses, so API
/// consumers always get a consistent, structured error payload.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            await WriteProblemAsync(context, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteUnexpectedProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, DomainException exception)
    {
        if (exception.StatusCode >= 500)
        {
            _logger.LogError(exception, "Domain error ({Code}) while processing {Method} {Path}",
                exception.Code, context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogWarning("Domain error ({Code}, {StatusCode}): {Message}",
                exception.Code, exception.StatusCode, exception.Message);
        }

        var problem = new ProblemDetails
        {
            Title = exception.Code,
            Detail = exception.Message,
            Status = exception.StatusCode,
            Instance = context.Request.Path,
        };
        problem.Extensions["correlationId"] = context.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var cid)
            ? cid
            : null;

        if (exception is ValidationDomainException validationException)
        {
            problem.Extensions["errors"] = validationException.Errors;
        }

        context.Response.StatusCode = exception.StatusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem), context.RequestAborted);
    }

    private async Task WriteUnexpectedProblemAsync(HttpContext context, Exception exception)
    {
        var problem = new ProblemDetails
        {
            Title = "unexpected_error",
            Detail = _environment.IsDevelopment() ? exception.ToString() : "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
            Instance = context.Request.Path,
        };
        problem.Extensions["correlationId"] = context.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var cid)
            ? cid
            : null;

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem), context.RequestAborted);
    }
}
