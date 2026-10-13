using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TechCurse.Api.IntegrationTests.Infraestrutura;
using TechCurse.Infrastructure.Data;

namespace TechCurse.Api.IntegrationTests.Autenticacao;

public sealed class RenovacaoDeSessaoTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    private const string MensagemDeRefreshInvalido = "Refresh Token inválido ou expirado.";

    private Task<HttpResponseMessage> RenovarAsync(HttpClient cliente, string? refreshToken) =>
        cliente.SendAsync(ComCookieDeSessao(HttpMethod.Post, "/tech-curse/Auth/refresh", refreshToken), Cancelamento);

    private Task<HttpResponseMessage> RenovarAsync(string? refreshToken) =>
        RenovarAsync(Cliente, refreshToken);

    private Task<HttpResponseMessage> SairAsync(string? refreshToken) =>
        Cliente.SendAsync(ComCookieDeSessao(HttpMethod.Post, "/tech-curse/Auth/logout", refreshToken), Cancelamento);

    private static string RefreshTokenDaResposta(HttpResponseMessage resposta) => ValorDoCookieDeSessao(resposta);

    private static string[] AtributosDoCookie(HttpResponseMessage resposta) =>
        CabecalhoDoCookieDeSessao(resposta)!.Split(';').Skip(1).Select(atributo => atributo.Trim().ToLowerInvariant()).ToArray();

    private async Task<(string? Motivo, bool Ativa)[]> SessoesAsync(string email)
    {
        using var escopo = Ambiente.Fabrica.Services.CreateScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var usuario = await usuarios.FindByEmailAsync(email);
        var contexto = escopo.ServiceProvider.GetRequiredService<TechCurseContext>();
        return (await contexto.SessoesDeAutenticacao
                .Where(sessao => sessao.UsuarioId == usuario!.Id)
                .Select(sessao => new { sessao.MotivoDaRevogacao, sessao.RevogadaEm })
                .ToListAsync(Cancelamento))
            .Select(sessao => (sessao.MotivoDaRevogacao, sessao.RevogadaEm == null))
            .ToArray();
    }

    private async Task<string[]> HashesGuardadosAsync(string email)
    {
        using var escopo = Ambiente.Fabrica.Services.CreateScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var usuario = await usuarios.FindByEmailAsync(email);
        var contexto = escopo.ServiceProvider.GetRequiredService<TechCurseContext>();
        return await contexto.RefreshTokensDeSessao
            .Where(token => token.Sessao.UsuarioId == usuario!.Id)
            .Select(token => token.Hash)
            .ToArrayAsync(Cancelamento);
    }

    private static string IdDoUsuario(string accessToken) =>
        new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims.First(claim => claim.Type == "nameid").Value;

    [Fact]
    [Trait("Especificacao", "AUTH-023")]
    public async Task Refresh_valido_devolve_um_novo_par_que_funciona()
    {
        var sessao = await RegistrarEEntrarAsync();

        var resposta = await RenovarAsync(sessao.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        var novoAccessToken = corpo.GetProperty("accessToken").GetString()!;
        Assert.NotEqual(sessao.RefreshToken, RefreshTokenDaResposta(resposta));
        var perfil = await ClienteAutenticado(novoAccessToken).GetAsync("/tech-curse/Student/me", Cancelamento);
        Assert.Equal(HttpStatusCode.OK, perfil.StatusCode);
    }

    [Fact]
    [Trait("Especificacao", "AUTH-024")]
    public async Task Refresh_token_usado_uma_vez_nao_vale_de_novo()
    {
        var sessao = await RegistrarEEntrarAsync();
        Assert.Equal(HttpStatusCode.OK, (await RenovarAsync(sessao.RefreshToken)).StatusCode);

        var reuso = await RenovarAsync(sessao.RefreshToken);

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

        var resposta = await RenovarAsync(cliente, RefreshTokenDaResposta(login));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal(MensagemDeRefreshInvalido, await DetalheDoErroAsync(resposta));
        Assert.Equal([("expirada", false)], await SessoesAsync(email));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-026")]
    public async Task Refresh_token_errado_e_recusado()
    {
        var sessao = await RegistrarEEntrarAsync();

        var resposta = await RenovarAsync(Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
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

        var resposta = await RenovarAsync(sessao.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal(MensagemDeRefreshInvalido, await DetalheDoErroAsync(resposta));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-030")]
    public async Task Refresh_token_fica_guardado_so_como_hash()
    {
        var sessao = await RegistrarEEntrarAsync();

        var guardados = await HashesGuardadosAsync(sessao.Email);

        Assert.DoesNotContain(sessao.RefreshToken, guardados);
        Assert.Equal([Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(sessao.RefreshToken)))], guardados);
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

        var resposta = await RenovarAsync(aluno.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal(MensagemDeRefreshInvalido, await DetalheDoErroAsync(resposta));
        Assert.Equal([("conta-bloqueada", false)], await SessoesAsync(aluno.Email));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-031")]
    public async Task Varias_sessoes_por_usuario_convivem_e_encerram_uma_a_uma()
    {
        var celular = await RegistrarEEntrarAsync();
        var computador = await EntrarAsync(celular.Email);

        var renovacaoDoCelular = await RenovarAsync(celular.RefreshToken);
        var renovacaoDoComputador = await RenovarAsync(computador.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, renovacaoDoCelular.StatusCode);
        Assert.Equal(HttpStatusCode.OK, renovacaoDoComputador.StatusCode);

        await SairAsync(RefreshTokenDaResposta(renovacaoDoCelular));

        var computadorDepois = await RenovarAsync(RefreshTokenDaResposta(renovacaoDoComputador));
        Assert.Equal(HttpStatusCode.OK, computadorDepois.StatusCode);
        Assert.Equal(1, (await SessoesAsync(celular.Email)).Count(sessao => sessao.Ativa));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-032")]
    public async Task Logout_revoga_a_sessao_na_hora()
    {
        var sessao = await RegistrarEEntrarAsync();

        var saida = await SairAsync(sessao.RefreshToken);
        var renovacao = await RenovarAsync(sessao.RefreshToken);

        Assert.Equal(HttpStatusCode.NoContent, saida.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, renovacao.StatusCode);
        Assert.Equal([("logout", false)], await SessoesAsync(sessao.Email));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-032")]
    public async Task Logout_de_token_desconhecido_ou_repetido_responde_204()
    {
        var sessao = await RegistrarEEntrarAsync();
        await SairAsync(sessao.RefreshToken);

        Assert.Equal(HttpStatusCode.NoContent, (await SairAsync(sessao.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SairAsync(Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)))).StatusCode);
    }

    [Fact]
    [Trait("Especificacao", "AUTH-033")]
    public async Task Reuso_de_refresh_token_revoga_a_sessao_inteira()
    {
        var sessao = await RegistrarEEntrarAsync();
        var renovacao = await RenovarAsync(sessao.RefreshToken);
        var refreshTokenNovo = RefreshTokenDaResposta(renovacao);

        var reuso = await RenovarAsync(sessao.RefreshToken);
        var tokenNovoDepois = await RenovarAsync(refreshTokenNovo);

        Assert.Equal(HttpStatusCode.Unauthorized, reuso.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, tokenNovoDepois.StatusCode);
        Assert.Equal([("reuso", false)], await SessoesAsync(sessao.Email));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-033")]
    public async Task Renovacoes_simultaneas_com_o_mesmo_token_nao_geram_duas_sessoes_validas()
    {
        var sessao = await RegistrarEEntrarAsync();
        var clientes = Enumerable.Range(0, 5).Select(_ => Ambiente.Fabrica.CreateClient()).ToArray();

        var respostas = await Task.WhenAll(clientes.Select(cliente => RenovarAsync(cliente, sessao.RefreshToken)));

        Assert.True(respostas.Count(resposta => resposta.StatusCode == HttpStatusCode.OK) <= 1);
        Assert.Contains(respostas, resposta => resposta.StatusCode == HttpStatusCode.Unauthorized);
        Assert.Equal([("reuso", false)], await SessoesAsync(sessao.Email));
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

    [Fact]
    [Trait("Especificacao", "AUTH-034")]
    public async Task Refresh_token_viaja_so_num_cookie_HttpOnly()
    {
        var email = NovoEmail();
        await RegistrarEEntrarAsync(email);

        var login = await Cliente.PostAsJsonAsync("/tech-curse/Auth/login", new { email, password = SenhaValida }, Cancelamento);

        var corpo = await login.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        Assert.False(corpo.TryGetProperty("refreshToken", out _));
        var atributos = AtributosDoCookie(login);
        Assert.Contains("httponly", atributos);
        Assert.Contains("secure", atributos);
        Assert.Contains("samesite=strict", atributos);
        Assert.Contains("path=/tech-curse/auth", atributos);
        Assert.Contains(atributos, atributo => atributo.StartsWith("expires=", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-034")]
    public async Task Renovacao_troca_o_cookie_e_logout_o_apaga()
    {
        var sessao = await RegistrarEEntrarAsync();

        var renovacao = await RenovarAsync(sessao.RefreshToken);
        var corpo = await renovacao.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        var saida = await SairAsync(RefreshTokenDaResposta(renovacao));

        Assert.Equal(HttpStatusCode.OK, renovacao.StatusCode);
        Assert.False(string.IsNullOrEmpty(corpo.GetProperty("accessToken").GetString()));
        Assert.False(corpo.TryGetProperty("refreshToken", out _));
        Assert.NotEqual(sessao.RefreshToken, RefreshTokenDaResposta(renovacao));
        Assert.Equal(HttpStatusCode.NoContent, saida.StatusCode);
        Assert.Contains("expires=thu, 01 jan 1970 00:00:00 gmt", AtributosDoCookie(saida));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-034")]
    public async Task Refresh_sem_cookie_ou_recusado_responde_401_e_apaga_o_cookie()
    {
        var semCookie = await RenovarAsync((string?)null);
        var invalido = await RenovarAsync(Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)));

        Assert.Equal(HttpStatusCode.Unauthorized, semCookie.StatusCode);
        Assert.Equal(MensagemDeRefreshInvalido, await DetalheDoErroAsync(semCookie));
        Assert.Equal(HttpStatusCode.Unauthorized, invalido.StatusCode);
        Assert.Contains("expires=thu, 01 jan 1970 00:00:00 gmt", AtributosDoCookie(invalido));
    }

    [Fact]
    [Trait("Especificacao", "AUTH-034")]
    public async Task Validade_do_access_token_vem_da_configuracao()
    {
        await using var fabrica = Ambiente.Fabrica.WithWebHostBuilder(builder =>
            builder.UseSetting("Jwt:AccessTokenMinutes", "5"));
        using var cliente = fabrica.CreateClient();
        var email = NovoEmail();
        await cliente.PostAsJsonAsync("/tech-curse/Auth/register",
            new { name = "AlunoDeCincoMinutos", email, password = SenhaValida, confirmPassword = SenhaValida }, Cancelamento);
        var antes = DateTime.UtcNow;

        var login = await cliente.PostAsJsonAsync("/tech-curse/Auth/login", new { email, password = SenhaValida }, Cancelamento);

        var expiraEm = (await login.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)).GetProperty("expiresAt").GetDateTime().ToUniversalTime();
        Assert.InRange(expiraEm, antes.AddMinutes(5).AddSeconds(-5), DateTime.UtcNow.AddMinutes(5).AddSeconds(5));
    }
}
