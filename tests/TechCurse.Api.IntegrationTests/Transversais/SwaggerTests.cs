using System.Net;
using Microsoft.AspNetCore.Hosting;
using TechCurse.Api.IntegrationTests.Infraestrutura;

namespace TechCurse.Api.IntegrationTests.Transversais;

public sealed class SwaggerTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    [Fact]
    [Trait("Especificacao", "TRV-026")]
    public async Task Swagger_ligado_pela_chave_responde()
    {
        await using var fabrica = Ambiente.Fabrica.WithWebHostBuilder(builder => builder.UseSetting("Swagger:Enabled", "true"));
        using var cliente = fabrica.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/swagger/v1/swagger.json", Cancelamento)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/swagger/index.html", Cancelamento)).StatusCode);
    }

    [Theory]
    [Trait("Especificacao", "TRV-026")]
    [InlineData("Testing")]
    [InlineData("Development")]
    [InlineData("Staging")]
    public async Task Sem_a_chave_o_Swagger_nao_existe_em_nenhum_ambiente(string ambienteDaApi)
    {
        await using var fabrica = Ambiente.Fabrica.WithWebHostBuilder(builder => builder.UseEnvironment(ambienteDaApi));
        using var cliente = fabrica.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/swagger/v1/swagger.json", Cancelamento)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/swagger/index.html", Cancelamento)).StatusCode);
    }
}
