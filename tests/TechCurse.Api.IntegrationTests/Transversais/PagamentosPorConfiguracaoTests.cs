using System.Net;
using Microsoft.AspNetCore.Hosting;
using TechCurse.Api.IntegrationTests.Infraestrutura;

namespace TechCurse.Api.IntegrationTests.Transversais;

public sealed class PagamentosPorConfiguracaoTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    [Fact]
    [Trait("Especificacao", "TRV-025")]
    public async Task Com_a_chave_ligada_as_rotas_de_pagamento_existem()
    {
        using var cliente = Ambiente.Fabrica.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/tech-curse/Payment", Cancelamento)).StatusCode);
    }

    [Fact]
    [Trait("Especificacao", "TRV-025")]
    public async Task Com_a_chave_desligada_as_rotas_de_pagamento_respondem_404_e_somem_do_Swagger()
    {
        await using var fabrica = Ambiente.Fabrica.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Payments:Enabled", "false");
            builder.UseSetting("Swagger:Enabled", "true");
        });
        using var cliente = fabrica.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/tech-curse/Payment", Cancelamento)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/tech-curse/Payment/1", Cancelamento)).StatusCode);

        var documento = await cliente.GetStringAsync("/swagger/v1/swagger.json", Cancelamento);
        Assert.DoesNotContain("/tech-curse/Payment", documento);
        Assert.Contains("/tech-curse/Course", documento);
    }

    [Fact]
    [Trait("Especificacao", "TRV-033")]
    public async Task Em_Production_com_os_pagamentos_desligados_a_API_sobe()
    {
        await using var fabrica = Ambiente.Fabrica.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Payments:Enabled", "false");
        });
        using var cliente = fabrica.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/health/live", Cancelamento)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/tech-curse/Payment", Cancelamento)).StatusCode);
    }

    [Fact]
    [Trait("Especificacao", "TRV-033")]
    public async Task Em_Production_com_os_pagamentos_ligados_a_API_nao_sobe()
    {
        await using var fabrica = Ambiente.Fabrica.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Payments:Enabled", "true");
        });

        var erro = Assert.ThrowsAny<Exception>(() => fabrica.CreateClient());

        var mensagens = string.Join(" | ", Cadeia(erro).Select(e => e.Message));
        Assert.Contains("Payments:Enabled", mensagens);
        Assert.Contains("nenhum gateway de pagamento real", mensagens);
    }

    private static IEnumerable<Exception> Cadeia(Exception erro)
    {
        for (var atual = erro; atual is not null; atual = atual.InnerException)
        {
            yield return atual;
        }
    }
}
