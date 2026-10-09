using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TechCurse.Api.IntegrationTests.Infraestrutura;

namespace TechCurse.Api.IntegrationTests.Autenticacao;

public sealed class RenovacaoDeSessaoTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    private const string MensagemDeRefreshInvalido = "Refresh Token inválido ou expirado.";

    private Task<HttpResponseMessage> RenovarAsync(HttpClient cliente, string accessToken, string refreshToken) =>
        cliente.PostAsJsonAsync("/tech-curse/Auth/refresh", new { accessToken, refreshToken }, Cancelamento);

    private Task<HttpResponseMessage> RenovarAsync(string accessToken, string refreshToken) =>
        RenovarAsync(Cliente, accessToken, refreshToken);

    private async Task<string?> RefreshTokenGuardadoAsync(string email)
    {
        using var escopo = Ambiente.Fabrica.Services.CreateScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var usuario = await usuarios.FindByEmailAsync(email);
        return await usuarios.GetAuthenticationTokenAsync(usuario!, "JWTApp", "RefreshToken");
    }

    private string TokenAssinado(string usuarioId, string email, byte[] chave, string algoritmo)
    {
        var descritor = new SecurityTokenDescriptor
        {
            Issuer = TechCurseApiFactory.Emissor,
            Audience = TechCurseApiFactory.Audiencia,
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, usuarioId),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, "Student")
            ]),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(chave), algoritmo)
        };

        var manipulador = new JwtSecurityTokenHandler();
        return manipulador.WriteToken(manipulador.CreateToken(descritor));
    }

    private static string IdDoUsuario(string accessToken) =>
        new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims.First(claim => claim.Type == "nameid").Value;

    [Fact]
    [Trait("Especificacao", "AUTH-023")]
    public async Task Refresh_valido_devolve_um_novo_par_que_funciona()
    {
        var sessao = await RegistrarEEntrarAsync();

        var resposta = await RenovarAsync(sessao.AccessToken, sessao.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        var novoAccessToken = corpo.GetProperty("accessToken").GetString()!;
        Assert.NotEqual(sessao.RefreshToken, corpo.GetProperty("refreshToken").GetString());
        var perfil = await ClienteAutenticado(novoAccessToken).GetAsync("/tech-curse/Student/me", Cancelamento);
        Assert.Equal(HttpStatusCode.OK, perfil.StatusCode);
    }

    [Fact]
    [Trait("Especificacao", "AUTH-024")]
    public async Task Refresh_token_usado_uma_vez_nao_vale_de_novo()
    {
        var sessao = await RegistrarEEntrarAsync();
        Assert.Equal(HttpStatusCode.OK, (await RenovarAsync(sessao.AccessToken, sessao.RefreshToken)).StatusCode);

        var reuso = await RenovarAsync(sessao.AccessToken, sessao.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, reuso.StatusCode);
    }

    [Fact]
    [Trait("Especificacao", "AUTH-025")]
    public async Task Refresh_token_vencido_encerra_a_sessao()
    {
        await using var fabricaComRefreshVencido = Ambiente.Fabrica.WithWebHostBuilder(builder =>
            builder.UseSetting("Jwt:RefreshTokenDays", "-1"));
        using var cliente = fabricaComRefreshVencido.CreateClient();
        var email = NovoEmail();
        await cliente.PostAsJsonAsync("/tech-curse/Auth/register",
            new { name = "AlunoVencido", email, password = SenhaValida, confirmPassword = SenhaValida }, Cancelamento);
        var login = await cliente.PostAsJsonAsync("/tech-curse/Auth/login", new { email, password = SenhaValida }, Cancelamento);
        var corpo = await login.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);

        var resposta = await RenovarAsync(cliente,
            corpo.GetProperty("accessToken").GetString()!, corpo.GetProperty("refreshToken").GetString()!);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal(MensagemDeRefreshInvalido, await DetalheDoErroAsync(resposta));
        Assert.Null(await RefreshTokenGuardadoAsync(email));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-026")]
    public async Task Refresh_token_errado_e_recusado()
    {
        var sessao = await RegistrarEEntrarAsync();

        var resposta = await RenovarAsync(sessao.AccessToken, Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    [Trait("Especificacao", "AUTH-027")]
    public async Task Access_token_malformado_no_refresh_responde_401()
    {
        var sessao = await RegistrarEEntrarAsync();

        var resposta = await RenovarAsync("nao-e-um-jwt", sessao.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal(MensagemDeRefreshInvalido, await DetalheDoErroAsync(resposta));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-027")]
    public async Task Access_token_com_assinatura_de_outra_chave_responde_401()
    {
        var sessao = await RegistrarEEntrarAsync();
        var adulterado = TokenAssinado(IdDoUsuario(sessao.AccessToken), sessao.Email,
            RandomNumberGenerator.GetBytes(48), SecurityAlgorithms.HmacSha256);

        var resposta = await RenovarAsync(adulterado, sessao.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal(MensagemDeRefreshInvalido, await DetalheDoErroAsync(resposta));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-027")]
    public async Task Access_token_com_outro_algoritmo_responde_401()
    {
        var sessao = await RegistrarEEntrarAsync();
        var outroAlgoritmo = TokenAssinado(IdDoUsuario(sessao.AccessToken), sessao.Email,
            Encoding.UTF8.GetBytes(Ambiente.Fabrica.ChaveDeAssinatura), SecurityAlgorithms.HmacSha512);

        var resposta = await RenovarAsync(outroAlgoritmo, sessao.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal(MensagemDeRefreshInvalido, await DetalheDoErroAsync(resposta));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-028")]
    public async Task Usuario_removido_nao_renova_a_sessao()
    {
        var sessao = await RegistrarEEntrarAsync();
        using (var escopo = Ambiente.Fabrica.Services.CreateScope())
        {
            var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var usuario = await usuarios.FindByEmailAsync(sessao.Email);
            await usuarios.DeleteAsync(usuario!);
        }

        var resposta = await RenovarAsync(sessao.AccessToken, sessao.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal(MensagemDeRefreshInvalido, await DetalheDoErroAsync(resposta));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-029")]
    public async Task Novo_login_invalida_a_sessao_anterior()
    {
        var primeiraSessao = await RegistrarEEntrarAsync();
        await EntrarAsync(primeiraSessao.Email);

        var resposta = await RenovarAsync(primeiraSessao.AccessToken, primeiraSessao.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    [Trait("Especificacao", "AUTH-030")]
    public async Task Refresh_token_fica_guardado_so_como_hash()
    {
        var sessao = await RegistrarEEntrarAsync();

        var guardado = await RefreshTokenGuardadoAsync(sessao.Email);

        Assert.NotEqual(sessao.RefreshToken, guardado);
        Assert.Equal(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(sessao.RefreshToken))), guardado);
    }

    [Fact]
    [Trait("Especificacao", "AUTH-039")]
    [Trait("Especificacao", "ALU-016")]
    public async Task Aluno_removido_pelo_Admin_nao_renova_a_sessao()
    {
        var aluno = await RegistrarEEntrarAsync();
        var perfil = await ClienteAutenticado(aluno.AccessToken)
            .GetFromJsonAsync<JsonElement>("/tech-curse/Student/me", Cancelamento);
        var admin = await CriarUsuarioEEntrarAsync("Admin");
        var remocao = await ClienteAutenticado(admin.AccessToken)
            .DeleteAsync($"/tech-curse/Student/{perfil.GetProperty("id").GetInt32()}", Cancelamento);
        Assert.Equal(HttpStatusCode.NoContent, remocao.StatusCode);

        var resposta = await RenovarAsync(aluno.AccessToken, aluno.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal(MensagemDeRefreshInvalido, await DetalheDoErroAsync(resposta));
        Assert.Null(await RefreshTokenGuardadoAsync(aluno.Email));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-036")]
    public async Task Chave_de_assinatura_fora_do_ASCII_funciona()
    {
        await using var fabricaComChaveAcentuada = Ambiente.Fabrica.WithWebHostBuilder(builder =>
            builder.UseSetting("Jwt:SigningKey", "chave-de-assinatura-com-acentuação-ção-ãé-ü-ñ-0123456789"));
        using var cliente = fabricaComChaveAcentuada.CreateClient();
        var email = NovoEmail();
        await cliente.PostAsJsonAsync("/tech-curse/Auth/register",
            new { name = "AlunoAcentuado", email, password = SenhaValida, confirmPassword = SenhaValida }, Cancelamento);
        var login = await cliente.PostAsJsonAsync("/tech-curse/Auth/login", new { email, password = SenhaValida }, Cancelamento);
        var accessToken = (await login.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)).GetProperty("accessToken").GetString();

        using var requisicao = new HttpRequestMessage(HttpMethod.Get, "/tech-curse/Student/me");
        requisicao.Headers.Authorization = new("Bearer", accessToken);
        var perfil = await cliente.SendAsync(requisicao, Cancelamento);

        Assert.Equal(HttpStatusCode.OK, perfil.StatusCode);
    }
}
