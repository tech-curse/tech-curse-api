using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TechCurse.Api.Configuration;

public static class RateLimitingSetup
{
    public const string PoliticaAutenticacao = "autenticacao";

    private const int LimiteGlobalPadrao = 200;
    private const int JanelaGlobalSegundosPadrao = 60;
    private const int LimiteAutenticacaoPadrao = 10;
    private const int JanelaAutenticacaoSegundosPadrao = 60;

    public static IServiceCollection AddRateLimitingSetup(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var habilitado = configuration.GetValue("RateLimiting:Enabled", true);

        var limiteGlobal = configuration.GetValue("RateLimiting:GlobalPermitLimit", LimiteGlobalPadrao);
        var janelaGlobal = TimeSpan.FromSeconds(
            configuration.GetValue("RateLimiting:GlobalWindowSeconds", JanelaGlobalSegundosPadrao));

        var limiteAutenticacao = configuration.GetValue("RateLimiting:AuthPermitLimit", LimiteAutenticacaoPadrao);
        var janelaAutenticacao = TimeSpan.FromSeconds(
            configuration.GetValue("RateLimiting:AuthWindowSeconds", JanelaAutenticacaoSegundosPadrao));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = (int)HttpStatusCode.TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (!habilitado)
                {
                    return RateLimitPartition.GetNoLimiter("desabilitado");
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    ObterChaveDeParticao(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limiteGlobal,
                        Window = janelaGlobal,
                        QueueLimit = 0
                    });
            });

            options.AddPolicy(PoliticaAutenticacao, context =>
            {
                if (!habilitado)
                {
                    return RateLimitPartition.GetNoLimiter("desabilitado");
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    $"auth:{ObterEnderecoRemoto(context)}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limiteAutenticacao,
                        Window = janelaAutenticacao,
                        QueueLimit = 0
                    });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                var problemDetails = new ProblemDetails
                {
                    Detail = "Muitas requisições em um curto intervalo. Tente novamente mais tarde.",
                    Instance = context.HttpContext.Request.Path,
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = nameof(HttpStatusCode.TooManyRequests)
                };

                context.HttpContext.Response.ContentType = "application/problem+json";

                await context.HttpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(problemDetails), cancellationToken);
            };
        });

        return services;
    }

    private static string ObterChaveDeParticao(HttpContext context)
    {
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return string.IsNullOrEmpty(userId)
            ? $"ip:{ObterEnderecoRemoto(context)}"
            : $"user:{userId}";
    }

    private static string ObterEnderecoRemoto(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
}
