using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TechCurse.Api.IntegrationTests.Infraestrutura;

namespace TechCurse.Api.IntegrationTests.Transversais;

public sealed class TelemetriaTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    private const string EnderecoQueNaoResponde = "http://127.0.0.1:9";

    [Fact]
    [Trait("Especificacao", "TRV-038")]
    public async Task Sem_endereco_OTLP_nada_e_exportado_e_a_API_funciona()
    {
        Assert.Null(Ambiente.Fabrica.Services.GetService<TracerProvider>());
        Assert.Null(Ambiente.Fabrica.Services.GetService<MeterProvider>());
        Assert.Equal(HttpStatusCode.OK, (await Cliente.GetAsync("/health/live", Cancelamento)).StatusCode);
    }

    [Fact]
    [Trait("Especificacao", "TRV-038")]
    public async Task Com_endereco_OTLP_a_API_se_identifica_e_continua_respondendo_mesmo_sem_coletor()
    {
        await using var fabrica = Ambiente.Fabrica.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", EnderecoQueNaoResponde);
            builder.UseSetting("OTEL_RESOURCE_ATTRIBUTES", "deployment.environment=teste");
        });
        using var cliente = fabrica.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/health/live", Cancelamento)).StatusCode);

        var atributos = fabrica.Services.GetRequiredService<TracerProvider>().GetResource().Attributes
            .ToDictionary(atributo => atributo.Key, atributo => atributo.Value);
        Assert.Equal("tech-curse-api", atributos["service.name"]);
        Assert.False(string.IsNullOrWhiteSpace(atributos["service.version"] as string));
        Assert.Equal("teste", atributos["deployment.environment"]);
        Assert.NotNull(fabrica.Services.GetService<MeterProvider>());
    }

    [Fact]
    [Trait("Especificacao", "TRV-038")]
    public async Task Log_da_requisicao_leva_o_trace_dela_e_a_sonda_de_saude_nao_gera_trace()
    {
        var traces = new List<Activity>();
        var logs = new List<LogRecord>();
        await using var fabrica = Ambiente.Fabrica.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", EnderecoQueNaoResponde);
            builder.ConfigureTestServices(servicos =>
            {
                servicos.ConfigureOpenTelemetryTracerProvider(construtor => construtor.AddInMemoryExporter(traces));
                servicos.AddLogging(registro => registro.AddOpenTelemetry(opcoes => opcoes.AddInMemoryExporter(logs)));
            });
        });
        using var cliente = fabrica.CreateClient();
        var sessao = await RegistrarEEntrarAsync();
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, "/tech-curse/Course/999999");
        requisicao.Headers.Authorization = new("Bearer", sessao.AccessToken);

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/health/live", Cancelamento)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.SendAsync(requisicao, Cancelamento)).StatusCode);
        fabrica.Services.GetRequiredService<TracerProvider>().ForceFlush();
        fabrica.Services.GetRequiredService<LoggerProvider>().ForceFlush();

        Assert.DoesNotContain(traces, trace => trace.GetTagItem("url.path") is "/health/live");
        var traceDaRequisicao = Assert.Single(traces, trace => trace.GetTagItem("url.path") is "/tech-curse/Course/999999");
        var logDaRecusa = Assert.Single(logs, log => log.FormattedMessage?.Contains("Curso não encontrado.") == true);
        Assert.Equal(traceDaRequisicao.TraceId, logDaRecusa.TraceId);
        Assert.Contains(traces, trace => trace.Source.Name == "Npgsql" && trace.TraceId == traceDaRequisicao.TraceId);
    }
}
