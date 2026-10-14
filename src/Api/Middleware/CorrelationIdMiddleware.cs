using Serilog.Context;

namespace TechCurse.Api.Middleware;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    public const string CorrelationIdHeaderName = "X-Correlation-ID";

    public const int TamanhoMaximo = 64;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var recebido = context.Request.Headers[CorrelationIdHeaderName].FirstOrDefault();
        var correlationId = EhValido(recebido) ? recebido! : Guid.NewGuid().ToString();

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey(CorrelationIdHeaderName))
                {
                    context.Response.Headers.Append(CorrelationIdHeaderName, correlationId);
                }
                return Task.CompletedTask;
            });

            await _next(context);
        }
    }

    private static bool EhValido(string? valor) =>
        !string.IsNullOrEmpty(valor)
        && valor.Length <= TamanhoMaximo
        && valor.All(caractere => char.IsAsciiLetterOrDigit(caractere) || caractere == '-');
}
