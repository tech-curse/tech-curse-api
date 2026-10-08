using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TechCurse.Api.Middleware;

namespace TechCurse.Api.Configuration;

public static class CorsSetup
{
    public const string PoliticaFrontend = "frontend";

    private const string ChaveOrigensPermitidas = "Cors:AllowedOrigins";
    private const string CabecalhoRetryAfter = "Retry-After";

    public static IServiceCollection AddCorsSetup(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var origens = ObterOrigensPermitidas(configuration);

        services.AddCors(options =>
        {
            options.AddPolicy(PoliticaFrontend, policy =>
            {
                if (origens.Length == 0)
                {
                    return;
                }

                policy
                    .WithOrigins(origens)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .WithExposedHeaders(
                        CorrelationIdMiddleware.CorrelationIdHeaderName,
                        CabecalhoRetryAfter);
            });
        });

        return services;
    }

    private static string[] ObterOrigensPermitidas(IConfiguration configuration)
    {
        var configurado = configuration.GetValue<string>(ChaveOrigensPermitidas);

        if (string.IsNullOrWhiteSpace(configurado))
        {
            return [];
        }

        return configurado
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(origem => origem.TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
