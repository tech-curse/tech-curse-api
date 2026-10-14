using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TechCurse.Api.Administracao;
using TechCurse.Api.IntegrationTests.Infraestrutura;

namespace TechCurse.Api.IntegrationTests.Transversais;

public sealed class ComandosAdministrativosTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    [Fact]
    [Trait("Especificacao", "TRV-024")]
    public async Task A_API_sobe_sem_alterar_o_banco_e_o_migrar_aplica_as_migrations()
    {
        var banco = $"sem_migrations_{Guid.NewGuid():N}";
        await ExecutarSqlAsync($"CREATE DATABASE \"{banco}\"");

        try
        {
            var conexao = new NpgsqlConnectionStringBuilder(Ambiente.ConexaoPostgres) { Database = banco }.ConnectionString;
            await using var fabrica = Ambiente.Fabrica.WithWebHostBuilder(builder =>
                builder.UseSetting("ConnectionStrings:APITechCurse", conexao));

            using (var cliente = fabrica.CreateClient())
            {
                Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/health/live", Cancelamento)).StatusCode);
            }

            Assert.False(await TabelaExisteAsync(conexao, "__EFMigrationsHistory"));

            Assert.Equal(0, await ComandosAdministrativos.ExecutarAsync([ComandosAdministrativos.Migrar], fabrica.Services, Cancelamento));
            Assert.Equal(0, await ComandosAdministrativos.ExecutarAsync([ComandosAdministrativos.Migrar], fabrica.Services, Cancelamento));

            Assert.True(await TabelaExisteAsync(conexao, "__EFMigrationsHistory"));
            Assert.True(await TabelaExisteAsync(conexao, "Courses"));

            using var escopo = fabrica.Services.CreateScope();
            var roles = escopo.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in new[] { "Admin", "Instructor", "Student" })
            {
                Assert.True(await roles.RoleExistsAsync(role));
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await ExecutarSqlAsync($"DROP DATABASE IF EXISTS \"{banco}\" WITH (FORCE)");
        }
    }

    [Fact]
    [Trait("Especificacao", "TRV-035")]
    public async Task Criar_admin_cria_o_Admin_com_as_credenciais_das_variaveis_de_ambiente()
    {
        var email = NovoEmail();
        await using var fabrica = FabricaComCredenciais(email, SenhaValida);

        Assert.Equal(0, await CriarAdminAsync(fabrica));

        using var escopo = fabrica.Services.CreateScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var usuario = await usuarios.FindByEmailAsync(email);
        Assert.NotNull(usuario);
        Assert.True(await usuarios.IsInRoleAsync(usuario, "Admin"));
        Assert.True(await usuarios.CheckPasswordAsync(usuario, SenhaValida));
    }

    [Fact]
    [Trait("Especificacao", "TRV-035")]
    public async Task Criar_admin_nao_altera_usuario_que_ja_existe()
    {
        var email = NovoEmail();
        await RegistrarEEntrarAsync(email);
        await using var fabrica = FabricaComCredenciais(email, "Outra@Senha123");

        Assert.Equal(0, await CriarAdminAsync(fabrica));

        using var escopo = fabrica.Services.CreateScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var usuario = await usuarios.FindByEmailAsync(email);
        Assert.NotNull(usuario);
        Assert.False(await usuarios.IsInRoleAsync(usuario, "Admin"));
        Assert.True(await usuarios.CheckPasswordAsync(usuario, SenhaValida));
    }

    [Theory]
    [Trait("Especificacao", "TRV-035")]
    [InlineData("curta1!A")]
    [InlineData("semnumerosnemsimbolos")]
    public async Task Criar_admin_com_senha_fora_da_politica_falha_sem_criar_usuario(string senha)
    {
        var email = NovoEmail();
        await using var fabrica = FabricaComCredenciais(email, senha);

        Assert.NotEqual(0, await CriarAdminAsync(fabrica));
        Assert.False(await UsuarioExisteAsync(fabrica, email));
    }

    [Fact]
    [Trait("Especificacao", "TRV-035")]
    public async Task Criar_admin_sem_credenciais_falha()
    {
        Assert.NotEqual(0, await ComandosAdministrativos.ExecutarAsync([ComandosAdministrativos.CriarAdmin], Ambiente.Fabrica.Services, Cancelamento));
    }

    [Fact]
    [Trait("Especificacao", "TRV-035")]
    public async Task Criar_admin_recusa_argumentos_na_linha_de_comando()
    {
        var email = NovoEmail();
        await using var fabrica = FabricaComCredenciais(email, SenhaValida);

        Assert.NotEqual(0, await ComandosAdministrativos.ExecutarAsync([ComandosAdministrativos.CriarAdmin, SenhaValida], fabrica.Services, Cancelamento));
        Assert.False(await UsuarioExisteAsync(fabrica, email));
    }

    private WebApplicationFactory<Program> FabricaComCredenciais(string email, string senha) =>
        Ambiente.Fabrica.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(ComandosAdministrativos.ChaveEmailDoAdmin, email);
            builder.UseSetting(ComandosAdministrativos.ChaveSenhaDoAdmin, senha);
        });

    private static Task<int> CriarAdminAsync(WebApplicationFactory<Program> fabrica) =>
        ComandosAdministrativos.ExecutarAsync([ComandosAdministrativos.CriarAdmin], fabrica.Services, Cancelamento);

    private static async Task<bool> UsuarioExisteAsync(WebApplicationFactory<Program> fabrica, string email)
    {
        using var escopo = fabrica.Services.CreateScope();
        return await escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync(email) is not null;
    }

    private async Task ExecutarSqlAsync(string sql)
    {
        await using var conexao = new NpgsqlConnection(Ambiente.ConexaoPostgres);
        await conexao.OpenAsync(Cancelamento);
        await using var comando = new NpgsqlCommand(sql, conexao);
        await comando.ExecuteNonQueryAsync(Cancelamento);
    }

    private static async Task<bool> TabelaExisteAsync(string conexao, string tabela)
    {
        await using var conexaoAberta = new NpgsqlConnection(conexao);
        await conexaoAberta.OpenAsync(Cancelamento);
        await using var comando = new NpgsqlCommand("SELECT to_regclass(@nome) IS NOT NULL", conexaoAberta);
        comando.Parameters.AddWithValue("nome", $"public.\"{tabela}\"");
        return (bool)(await comando.ExecuteScalarAsync(Cancelamento))!;
    }
}
