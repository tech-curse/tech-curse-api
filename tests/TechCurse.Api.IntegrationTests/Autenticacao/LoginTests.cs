using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TechCurse.Api.IntegrationTests.Infraestrutura;

namespace TechCurse.Api.IntegrationTests.Autenticacao;

public sealed class LoginTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    private const string MensagemDeCredenciaisInvalidas = "E-mail ou senha incorretos.";

    private Task<HttpResponseMessage> EntrarComAsync(string email, string senha) =>
        Cliente.PostAsJsonAsync("/tech-curse/Auth/login", new { email, password = senha }, Cancelamento);

    private byte[] ChaveDaApi => Encoding.UTF8.GetBytes(Ambiente.Fabrica.ChaveDeAssinatura);

    private static string IdDoUsuario(string accessToken) =>
        new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims.First(claim => claim.Type == "nameid").Value;

    [Fact]
    [Trait("Especificacao", "AUTH-014")]
    public async Task Credenciais_validas_devolvem_um_par_de_tokens_que_vale_2_horas()
    {
        var email = NovoEmail();
        await RegistrarEEntrarAsync(email);
        var antes = DateTime.UtcNow;

        var resposta = await EntrarComAsync(email, SenhaValida);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        Assert.False(string.IsNullOrEmpty(corpo.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrEmpty(corpo.GetProperty("refreshToken").GetString()));
        var expiraEm = corpo.GetProperty("expiresAt").GetDateTime().ToUniversalTime();
        Assert.InRange(expiraEm, antes.AddHours(2).AddSeconds(-5), DateTime.UtcNow.AddHours(2).AddSeconds(5));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-015")]
    public async Task Access_token_carrega_identidade_e_papel()
    {
        var sessao = await RegistrarEEntrarAsync();

        var token = new JwtSecurityTokenHandler().ReadJwtToken(sessao.AccessToken);

        Assert.Equal("HS256", token.Header.Alg);
        Assert.Equal(TechCurseApiFactory.Emissor, token.Issuer);
        Assert.Equal([TechCurseApiFactory.Audiencia], token.Audiences);
        Assert.Equal(sessao.Email, token.Claims.First(claim => claim.Type == "email").Value);
        Assert.Equal("Student", token.Claims.First(claim => claim.Type == "role").Value);
        using var escopo = Ambiente.Fabrica.Services.CreateScope();
        var usuario = await escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync(sessao.Email);
        Assert.Equal(usuario!.Id, token.Claims.First(claim => claim.Type == "nameid").Value);
    }

    [Fact]
    [Trait("Especificacao", "AUTH-016")]
    public async Task Credenciais_erradas_nao_revelam_se_o_email_existe()
    {
        var email = NovoEmail();
        await RegistrarEEntrarAsync(email);

        var emailInexistente = await EntrarComAsync(NovoEmail(), SenhaValida);
        var senhaErrada = await EntrarComAsync(email, "Outra@Senha1");

        Assert.Equal(HttpStatusCode.Unauthorized, emailInexistente.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, senhaErrada.StatusCode);
        Assert.Equal(MensagemDeCredenciaisInvalidas, await DetalheDoErroAsync(emailInexistente));
        Assert.Equal(MensagemDeCredenciaisInvalidas, await DetalheDoErroAsync(senhaErrada));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-017")]
    public async Task Login_nao_depende_de_confirmacao_de_email()
    {
        var email = NovoEmail();
        await Cliente.PostAsJsonAsync("/tech-curse/Auth/register",
            new { name = "Ana", email, password = SenhaValida, confirmPassword = SenhaValida }, Cancelamento);

        Assert.Equal(HttpStatusCode.OK, (await EntrarComAsync(email, SenhaValida)).StatusCode);
    }

    [Theory]
    [Trait("Especificacao", "AUTH-019")]
    [InlineData(null)]
    [InlineData("nao-e-um-jwt")]
    public async Task Endpoint_protegido_exige_token(string? token)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, "/tech-curse/Student/me");
        if (token is not null)
        {
            requisicao.Headers.Authorization = new("Bearer", token);
        }

        var resposta = await Cliente.SendAsync(requisicao, Cancelamento);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal("Acesso negado. Token ausente ou inválido.", await DetalheDoErroAsync(resposta));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-020")]
    public async Task Token_vencido_ha_poucos_segundos_e_recusado()
    {
        var sessao = await RegistrarEEntrarAsync();
        var vencido = TokenAssinado(IdDoUsuario(sessao.AccessToken), sessao.Email, "Student", ChaveDaApi,
            SecurityAlgorithms.HmacSha256, DateTime.UtcNow.AddSeconds(-5));

        var resposta = await ClienteAutenticado(vencido).GetAsync("/tech-curse/Student/me", Cancelamento);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    [Trait("Especificacao", "AUTH-021")]
    public async Task Papel_insuficiente_e_recusado()
    {
        var aluno = await RegistrarEEntrarAsync();

        var resposta = await ClienteAutenticado(aluno.AccessToken).GetAsync("/tech-curse/Student", Cancelamento);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal("Você não tem permissão para acessar este recurso.", await DetalheDoErroAsync(resposta));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-022")]
    public async Task Token_de_outro_emissor_nao_vale()
    {
        var sessao = await RegistrarEEntrarAsync();
        var id = IdDoUsuario(sessao.AccessToken);
        var daqui = DateTime.UtcNow.AddHours(1);
        string[] tokens =
        [
            TokenAssinado(id, sessao.Email, "Student", RandomNumberGenerator.GetBytes(48), SecurityAlgorithms.HmacSha256, daqui),
            TokenAssinado(id, sessao.Email, "Student", ChaveDaApi, SecurityAlgorithms.HmacSha256, daqui, emissor: "outro-emissor"),
            TokenAssinado(id, sessao.Email, "Student", ChaveDaApi, SecurityAlgorithms.HmacSha256, daqui, audiencia: "outra-audiencia")
        ];

        foreach (var token in tokens)
        {
            var resposta = await ClienteAutenticado(token).GetAsync("/tech-curse/Student/me", Cancelamento);
            Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        }
    }

    [Fact]
    [Trait("Especificacao", "AUTH-037")]
    public async Task Endpoints_de_autenticacao_tem_limite_de_requisicoes_proprio()
    {
        await using var fabrica = Ambiente.Fabrica.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:Enabled", "true");
            builder.UseSetting("RateLimiting:AuthPermitLimit", "2");
            builder.UseSetting("RateLimiting:AuthWindowSeconds", "60");
        });
        using var cliente = fabrica.CreateClient();
        var corpo = new { email = NovoEmail(), password = "Qualquer@1" };

        await cliente.PostAsJsonAsync("/tech-curse/Auth/login", corpo, Cancelamento);
        await cliente.PostAsJsonAsync("/tech-curse/Auth/login", corpo, Cancelamento);
        var excedente = await cliente.PostAsJsonAsync("/tech-curse/Auth/login", corpo, Cancelamento);

        Assert.Equal(HttpStatusCode.TooManyRequests, excedente.StatusCode);
        Assert.True(excedente.Headers.Contains("Retry-After"));
    }
}
