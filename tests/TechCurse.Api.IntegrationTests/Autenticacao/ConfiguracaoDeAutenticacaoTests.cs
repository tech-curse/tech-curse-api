using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TechCurse.Api.IntegrationTests.Infraestrutura;

namespace TechCurse.Api.IntegrationTests.Autenticacao;

public sealed class ConfiguracaoDeAutenticacaoTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    [Theory]
    [Trait("Especificacao", "AUTH-035")]
    [InlineData("")]
    [InlineData("chave-curta-com-31-caracteres!!")]
    public async Task API_nao_sobe_sem_chave_de_assinatura_forte(string chave)
    {
        await using var fabrica = Ambiente.Fabrica.WithWebHostBuilder(builder => builder.UseSetting("Jwt:SigningKey", chave));

        var erro = Record.Exception(() => fabrica.CreateClient());

        Assert.NotNull(erro);
        var raiz = erro;
        while (raiz.InnerException is not null)
        {
            raiz = raiz.InnerException;
        }

        Assert.IsType<InvalidOperationException>(raiz);
        Assert.Contains("SigningKey", raiz.Message);
    }

    [Theory]
    [Trait("Especificacao", "AUTH-038")]
    [InlineData("Development", true)]
    [InlineData("Staging", false)]
    [InlineData("Testing", false)]
    public async Task Admin_de_desenvolvimento_so_e_semeado_em_Development(string ambienteDaApi, bool deveExistir)
    {
        var email = $"admin-semeado-{Guid.NewGuid():N}@teste.dev";
        await using var fabrica = Ambiente.Fabrica.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(ambienteDaApi);
            builder.UseSetting("Seed:Admin:Email", email);
            builder.UseSetting("Seed:Admin:Password", SenhaValida);
        });

        fabrica.CreateClient().Dispose();

        using var escopo = Ambiente.Fabrica.Services.CreateScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var usuario = await usuarios.FindByEmailAsync(email);
        Assert.Equal(deveExistir, usuario is not null);
        if (deveExistir)
        {
            Assert.True(await usuarios.IsInRoleAsync(usuario!, "Admin"));
        }
    }
}
