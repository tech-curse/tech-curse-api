using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TechCurse.Api.IntegrationTests.Infraestrutura;
using TechCurse.Application.Interfaces;

namespace TechCurse.Api.IntegrationTests.Autenticacao;

public sealed class RegistroTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    private Task<HttpResponseMessage> RegistrarAsync(object corpo) =>
        Cliente.PostAsJsonAsync("/tech-curse/Auth/register", corpo, Cancelamento);

    private static object Corpo(string? email = null, string nome = "Ana", string senha = SenhaValida, string? confirmacao = null) =>
        new { name = nome, email = email ?? NovoEmail(), password = senha, confirmPassword = confirmacao ?? senha };

    private static async Task<JsonElement> ErrosAsync(HttpResponseMessage resposta) =>
        (await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)).GetProperty("errors");

    private async Task<bool> UsuarioExisteAsync(string email)
    {
        using var escopo = Ambiente.Fabrica.Services.CreateScope();
        return await escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync(email) is not null;
    }

    [Fact]
    [Trait("Especificacao", "AUTH-001")]
    public async Task Registro_valido_cria_um_aluno_pronto_para_usar_o_sistema()
    {
        var email = NovoEmail();

        var resposta = await RegistrarAsync(Corpo(email));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        Assert.Equal("Usuário registrado com sucesso.", corpo.GetProperty("mensagem").GetString());
        var sessao = await EntrarAsync(email);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(sessao.AccessToken);
        Assert.Equal("Student", token.Claims.First(claim => claim.Type == "role").Value);
        var perfil = await ClienteAutenticado(sessao.AccessToken).GetFromJsonAsync<JsonElement>("/tech-curse/Student/me", Cancelamento);
        Assert.Equal(email, perfil.GetProperty("email").GetString());
    }

    [Fact]
    [Trait("Especificacao", "AUTH-002")]
    public async Task Registro_publico_ignora_o_papel_enviado()
    {
        var email = NovoEmail();

        await RegistrarAsync(new { name = "Ana", email, role = "Admin", password = SenhaValida, confirmPassword = SenhaValida });

        var sessao = await EntrarAsync(email);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(sessao.AccessToken);
        Assert.Equal(["Student"], token.Claims.Where(claim => claim.Type == "role").Select(claim => claim.Value));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-003")]
    public async Task Senha_e_confirmacao_diferentes_sao_recusadas()
    {
        var email = NovoEmail();

        var resposta = await RegistrarAsync(Corpo(email, confirmacao: "Outra@Senha1"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Equal("A senha e a confirmação de senha não coincidem.", (await ErrosAsync(resposta)).GetProperty("Password")[0].GetString());
        Assert.False(await UsuarioExisteAsync(email));
    }

    [Theory]
    [Trait("Especificacao", "AUTH-004")]
    [InlineData("Ab1@", "PasswordTooShort")]
    [InlineData("SENHA@FORTE1", "PasswordRequiresLower")]
    [InlineData("senha@forte1", "PasswordRequiresUpper")]
    [InlineData("Senha@Forte", "PasswordRequiresDigit")]
    [InlineData("SenhaForte1", "PasswordRequiresNonAlphanumeric")]
    public async Task Senha_fora_da_politica_e_recusada_com_o_motivo(string senha, string codigo)
    {
        var resposta = await RegistrarAsync(Corpo(senha: senha));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.True((await ErrosAsync(resposta)).TryGetProperty(codigo, out _));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-005")]
    public async Task Email_de_usuario_existente_nao_pode_ser_usado_de_novo()
    {
        var email = NovoEmail();
        await RegistrarAsync(Corpo(email));

        var resposta = await RegistrarAsync(Corpo(email, nome: "Outra pessoa"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        var erros = await ErrosAsync(resposta);
        Assert.True(erros.TryGetProperty("DuplicateEmail", out _));
        Assert.False(erros.TryGetProperty("DuplicateUserName", out _));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-007")]
    public async Task Email_em_formato_invalido_e_recusado()
    {
        var resposta = await RegistrarAsync(Corpo("nao-e-um-email"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.True((await ErrosAsync(resposta)).TryGetProperty("InvalidEmail", out _));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-008")]
    public async Task Corpo_incompleto_responde_400()
    {
        var resposta = await RegistrarAsync(new { name = "Ana" });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Theory]
    [Trait("Especificacao", "AUTH-009")]
    [InlineData("João da Silva")]
    [InlineData("Ana")]
    public async Task Nome_livre_com_espaco_acento_e_repetido_e_aceito(string nome)
    {
        Assert.Equal(HttpStatusCode.Created, (await RegistrarAsync(Corpo(nome: nome))).StatusCode);

        var segundo = await RegistrarAsync(Corpo(nome: nome));

        Assert.Equal(HttpStatusCode.Created, segundo.StatusCode);
    }

    [Theory]
    [Trait("Especificacao", "AUTH-009")]
    [InlineData("   ", "O nome é obrigatório.")]
    [InlineData(null, "O nome deve ter no máximo 100 caracteres.")]
    public async Task Nome_vazio_ou_longo_demais_e_recusado(string? nome, string mensagem)
    {
        var resposta = await RegistrarAsync(Corpo(nome: nome ?? new string('a', 101)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Equal(mensagem, (await ErrosAsync(resposta)).GetProperty("Nome")[0].GetString());
    }

    [Fact]
    [Trait("Especificacao", "AUTH-010")]
    public async Task Falha_ao_gravar_o_perfil_desfaz_a_criacao_do_usuario()
    {
        await using var fabrica = Ambiente.Fabrica.WithWebHostBuilder(builder => builder.ConfigureTestServices(servicos =>
            servicos.AddScoped<IStudentRepository, RepositorioDeAlunosQueFalhaAoGravar>()));
        using var cliente = fabrica.CreateClient();
        var email = NovoEmail();

        var resposta = await cliente.PostAsJsonAsync("/tech-curse/Auth/register", Corpo(email), Cancelamento);

        Assert.Equal(HttpStatusCode.InternalServerError, resposta.StatusCode);
        Assert.False(await UsuarioExisteAsync(email));
    }

    [Theory]
    [Trait("Especificacao", "AUTH-011")]
    [InlineData("Admin")]
    [InlineData("Instructor")]
    [InlineData("Student")]
    public async Task Admin_cria_usuario_com_qualquer_papel(string papel)
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var email = NovoEmail();

        var resposta = await admin.PostAsJsonAsync("/tech-curse/Auth/users",
            new { name = "Pessoa Criada", email, role = papel, password = SenhaValida, confirmPassword = SenhaValida }, Cancelamento);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var sessao = await EntrarAsync(email);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(sessao.AccessToken);
        Assert.Equal(papel, token.Claims.First(claim => claim.Type == "role").Value);
        var perfil = await ClienteAutenticado(sessao.AccessToken).GetAsync("/tech-curse/Student/me", Cancelamento);
        Assert.Equal(papel == "Student" ? HttpStatusCode.OK : HttpStatusCode.Forbidden, perfil.StatusCode);
    }

    [Theory]
    [Trait("Especificacao", "AUTH-012")]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Student", HttpStatusCode.Forbidden)]
    [InlineData("Instructor", HttpStatusCode.Forbidden)]
    public async Task So_Admin_cria_usuarios(string? papel, HttpStatusCode esperado)
    {
        var cliente = papel is null ? Cliente : ClienteAutenticado((await CriarUsuarioEEntrarAsync(papel)).AccessToken);

        var resposta = await cliente.PostAsJsonAsync("/tech-curse/Auth/users",
            new { name = "Ninguem", email = NovoEmail(), role = "Admin", password = SenhaValida, confirmPassword = SenhaValida }, Cancelamento);

        Assert.Equal(esperado, resposta.StatusCode);
    }

    [Fact]
    [Trait("Especificacao", "AUTH-013")]
    public async Task Papel_desconhecido_e_recusado()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var emailTexto = NovoEmail();
        var emailNumero = NovoEmail();

        var texto = await admin.PostAsJsonAsync("/tech-curse/Auth/users",
            new { name = "X", email = emailTexto, role = "Root", password = SenhaValida, confirmPassword = SenhaValida }, Cancelamento);
        var numero = await admin.PostAsJsonAsync("/tech-curse/Auth/users",
            new { name = "X", email = emailNumero, role = 99, password = SenhaValida, confirmPassword = SenhaValida }, Cancelamento);

        Assert.Equal(HttpStatusCode.BadRequest, texto.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, numero.StatusCode);
        Assert.True((await ErrosAsync(numero)).TryGetProperty("Role", out _));
        Assert.False(await UsuarioExisteAsync(emailTexto));
        Assert.False(await UsuarioExisteAsync(emailNumero));
    }
}
