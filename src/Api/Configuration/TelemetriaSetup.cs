using System.Reflection;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace TechCurse.Api.Configuration;

public static class TelemetriaSetup
{
    public const string NomeDoServico = "tech-curse-api";

    public const string ChaveDoEndereco = "OTEL_EXPORTER_OTLP_ENDPOINT";

    public static WebApplicationBuilder AddTelemetriaSetup(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();

        if (string.IsNullOrWhiteSpace(builder.Configuration[ChaveDoEndereco]))
        {
            return builder;
        }

        var versao = typeof(TelemetriaSetup).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            .Split('+')[0] ?? "desconhecida";

        builder.Logging.AddOpenTelemetry(opcoes =>
        {
            opcoes.IncludeFormattedMessage = true;
            opcoes.IncludeScopes = true;
            opcoes.AddOtlpExporter();
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(recurso => recurso.AddService(NomeDoServico, serviceVersion: versao))
            .WithTracing(traces => traces
                .AddAspNetCoreInstrumentation(opcoes =>
                    opcoes.Filter = contexto => !contexto.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddOtlpExporter())
            .WithMetrics(metricas => metricas
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter());

        return builder;
    }
}
