using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    public const string MensagemDeErroInesperado = "Ocorreu um erro inesperado. Informe o código de correlação ao suporte.";

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
            var statusCode = StatusDa(ex);

            if (statusCode is HttpStatusCode.InternalServerError or HttpStatusCode.GatewayTimeout)
            {
                _logger.LogError(ex, "A requisição falhou com {StatusCode}.", (int)statusCode);
            }
            else
            {
                _logger.LogWarning("A requisição foi recusada com {StatusCode}: {Motivo}", (int)statusCode, ex.Message);
            }

            await HandleExceptionAsync(context, ex, statusCode);
        }
    }

    private static HttpStatusCode StatusDa(Exception exception) =>
        exception switch
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

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, HttpStatusCode statusCode)
    {
        var problemDetails = new ProblemDetails
        {
            Detail = statusCode == HttpStatusCode.InternalServerError ? MensagemDeErroInesperado : exception.Message,
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
