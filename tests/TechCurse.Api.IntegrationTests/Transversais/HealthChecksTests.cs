using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TechCurse.Api.IntegrationTests.Infraestrutura;

namespace TechCurse.Api.IntegrationTests.Transversais;

public sealed class HealthChecksTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    [Fact]
    [Trait("Especificacao", "TRV-020")]
    public async Task Liveness_responde_Healthy_em_texto()
    {
        var resposta = await Cliente.GetAsync("/health/live", Cancelamento);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Healthy", await resposta.Content.ReadAsStringAsync(Cancelamento));
    }

    [Fact]
    [Trait("Especificacao", "TRV-021")]
    public async Task Readiness_responde_Healthy_com_PostgreSQL_e_Redis_no_ar()
    {
        var resposta = await Cliente.GetAsync("/health/ready", Cancelamento);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        Assert.Equal("Healthy", corpo.GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Especificacao", "TRV-022")]
    public async Task Readiness_sem_token_traz_so_o_status()
    {
        var resposta = await Cliente.GetAsync("/health/ready", Cancelamento);

        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        var propriedades = corpo.EnumerateObject().Select(propriedade => propriedade.Name).ToArray();
        Assert.Equal(["status"], propriedades);
    }
}
