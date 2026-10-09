using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace TechCurse.Api.IntegrationTests.Infraestrutura;

public abstract class TesteDeIntegracao(AmbienteDeTeste ambiente) : IAsyncLifetime
{
    public const string SenhaValida = "Senha@Forte1";

    private readonly List<HttpClient> _clientes = [];

    protected AmbienteDeTeste Ambiente { get; } = ambiente;

    protected HttpClient Cliente { get; } = ambiente.Fabrica.CreateClient();

    protected static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => Ambiente.RestaurarEstadoAsync();

    public ValueTask DisposeAsync()
    {
        Cliente.Dispose();
        foreach (var cliente in _clientes)
        {
            cliente.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    protected static string NovoEmail() => $"aluno-{Guid.NewGuid():N}@teste.dev";

    protected HttpClient ClienteAutenticado(string accessToken)
    {
        var cliente = Ambiente.Fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        _clientes.Add(cliente);
        return cliente;
    }

    protected async Task<SessaoDeTeste> RegistrarEEntrarAsync(string? email = null, string nome = "AlunoDeTeste")
    {
        email ??= NovoEmail();

        var registro = await Cliente.PostAsJsonAsync("/tech-curse/Auth/register",
            new { name = nome, email, password = SenhaValida, confirmPassword = SenhaValida }, Cancelamento);
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);

        return await EntrarAsync(email);
    }

    protected async Task<SessaoDeTeste> CriarUsuarioEEntrarAsync(string papel)
    {
        var email = $"{papel.ToLowerInvariant()}-{Guid.NewGuid():N}@teste.dev";

        using (var escopo = Ambiente.Fabrica.Services.CreateScope())
        {
            var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var usuario = new IdentityUser { UserName = email.Split('@')[0], Email = email, EmailConfirmed = true };
            Assert.True((await usuarios.CreateAsync(usuario, SenhaValida)).Succeeded);
            Assert.True((await usuarios.AddToRoleAsync(usuario, papel)).Succeeded);
        }

        return await EntrarAsync(email);
    }

    protected async Task<SessaoDeTeste> EntrarAsync(string email, string senha = SenhaValida)
    {
        var resposta = await Cliente.PostAsJsonAsync("/tech-curse/Auth/login", new { email, password = senha }, Cancelamento);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        return new SessaoDeTeste(
            email,
            corpo.GetProperty("accessToken").GetString()!,
            corpo.GetProperty("refreshToken").GetString()!,
            corpo.GetProperty("expiresAt").GetDateTime());
    }

    protected static async Task<string?> DetalheDoErroAsync(HttpResponseMessage resposta)
    {
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        return corpo.TryGetProperty("detail", out var detalhe) ? detalhe.GetString() : null;
    }
}
