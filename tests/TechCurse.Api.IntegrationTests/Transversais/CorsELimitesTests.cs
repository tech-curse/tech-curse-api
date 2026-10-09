using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TechCurse.Api.IntegrationTests.Infraestrutura;

namespace TechCurse.Api.IntegrationTests.Transversais;

public sealed class CorsELimitesTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    private const string OrigemPermitida = "http://localhost:4200";

    private static HttpRequestMessage Preflight(string origem)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Options, "/tech-curse/Course");
        requisicao.Headers.Add("Origin", origem);
        requisicao.Headers.Add("Access-Control-Request-Method", "GET");
        requisicao.Headers.Add("Access-Control-Request-Headers", "authorization");
        return requisicao;
    }

    private static string? Cabecalho(HttpResponseMessage resposta, string nome) =>
        resposta.Headers.TryGetValues(nome, out var valores) ? string.Join(",", valores) : null;

    private WebApplicationFactory<Program> FabricaCom(params (string Chave, string Valor)[] configuracoes) =>
        Ambiente.Fabrica.WithWebHostBuilder(builder =>
        {
            foreach (var (chave, valor) in configuracoes)
            {
                builder.UseSetting(chave, valor);
            }
        });

    [Fact]
    [Trait("Especificacao", "TRV-007")]
    public async Task So_as_origens_configuradas_acessam_a_API()
    {
        var permitida = await Cliente.SendAsync(Preflight(OrigemPermitida), Cancelamento);
        var outra = await Cliente.SendAsync(Preflight("http://site-malicioso.test"), Cancelamento);

        Assert.Equal(OrigemPermitida, Cabecalho(permitida, "Access-Control-Allow-Origin"));
        Assert.Contains("GET", Cabecalho(permitida, "Access-Control-Allow-Methods"));
        Assert.Null(Cabecalho(outra, "Access-Control-Allow-Origin"));
    }

    [Fact]
    [Trait("Especificacao", "TRV-008")]
    public async Task Sem_origens_configuradas_nenhuma_origem_e_liberada()
    {
        await using var fabrica = FabricaCom(("Cors:AllowedOrigins", ""));
        using var cliente = fabrica.CreateClient();

        var resposta = await cliente.SendAsync(Preflight(OrigemPermitida), Cancelamento);

        Assert.Null(Cabecalho(resposta, "Access-Control-Allow-Origin"));
    }

    [Fact]
    [Trait("Especificacao", "TRV-009")]
    public async Task Origens_sao_normalizadas()
    {
        await using var fabrica = FabricaCom(("Cors:AllowedOrigins", " http://a.test/ , http://b.test "));
        using var cliente = fabrica.CreateClient();

        var a = await cliente.SendAsync(Preflight("http://a.test"), Cancelamento);
        var b = await cliente.SendAsync(Preflight("http://b.test"), Cancelamento);

        Assert.Equal("http://a.test", Cabecalho(a, "Access-Control-Allow-Origin"));
        Assert.Equal("http://b.test", Cabecalho(b, "Access-Control-Allow-Origin"));
    }

    [Fact]
    [Trait("Especificacao", "TRV-010")]
    public async Task Navegador_le_os_cabecalhos_de_correlacao_e_de_limite()
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        requisicao.Headers.Add("Origin", OrigemPermitida);

        var resposta = await Cliente.SendAsync(requisicao, Cancelamento);

        var expostos = Cabecalho(resposta, "Access-Control-Expose-Headers") ?? "";
        Assert.Contains("X-Correlation-ID", expostos);
        Assert.Contains("Retry-After", expostos);
        Assert.Null(Cabecalho(resposta, "Access-Control-Allow-Credentials"));
    }

    [Fact]
    [Trait("Especificacao", "TRV-011")]
    public async Task Preflight_nao_e_bloqueado_pelo_rate_limiting()
    {
        await using var fabrica = FabricaCom(("RateLimiting:Enabled", "true"), ("RateLimiting:GlobalPermitLimit", "1"));
        using var cliente = fabrica.CreateClient();
        await cliente.GetAsync("/health/live", Cancelamento);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await cliente.GetAsync("/health/live", Cancelamento)).StatusCode);

        var preflight = await cliente.SendAsync(Preflight(OrigemPermitida), Cancelamento);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, preflight.StatusCode);
        Assert.NotEqual(HttpStatusCode.TemporaryRedirect, preflight.StatusCode);
        Assert.Equal(OrigemPermitida, Cabecalho(preflight, "Access-Control-Allow-Origin"));
    }

    [Fact]
    [Trait("Especificacao", "TRV-012")]
    public async Task Limite_global_responde_429_com_Retry_After()
    {
        await using var fabrica = FabricaCom(("RateLimiting:Enabled", "true"), ("RateLimiting:GlobalPermitLimit", "2"));
        using var cliente = fabrica.CreateClient();

        await cliente.GetAsync("/health/live", Cancelamento);
        await cliente.GetAsync("/health/live", Cancelamento);
        var excedente = await cliente.GetAsync("/health/live", Cancelamento);

        Assert.Equal(HttpStatusCode.TooManyRequests, excedente.StatusCode);
        Assert.NotNull(Cabecalho(excedente, "Retry-After"));
        var corpo = JsonDocument.Parse(await excedente.Content.ReadAsStringAsync(Cancelamento)).RootElement;
        Assert.Equal("Muitas requisições em um curto intervalo. Tente novamente mais tarde.", corpo.GetProperty("detail").GetString());
    }
}
