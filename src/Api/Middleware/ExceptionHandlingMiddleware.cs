using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ocorreu uma exceção não tratada.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = exception switch
        {
            BadRequestException => HttpStatusCode.BadRequest,
            UnauthorizedException => HttpStatusCode.Unauthorized,
            ForbiddenAccessException => HttpStatusCode.Forbidden,
            NotFoundException => HttpStatusCode.NotFound,
            NotAllowedException => HttpStatusCode.Conflict,
            ConflictException => HttpStatusCode.Conflict,
            ValidationException => HttpStatusCode.UnprocessableEntity,
            GatewayTimeoutException => HttpStatusCode.GatewayTimeout,
            _ => HttpStatusCode.InternalServerError
        };

        var problemDetails = new ProblemDetails
        {
            Detail = exception.Message,
            Instance = context.Request.Path,
            Status = (int)statusCode,
            Title = statusCode.ToString(),
        };

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions.Add("errors", validationException.Errors);
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
    }
}
