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

    protected static async Task<int> CriarCursoAsync(HttpClient admin, string titulo = "Curso de teste", string categoria = "Backend")
    {
        var resposta = await admin.PostAsJsonAsync("/tech-curse/Course",
            new { titulo, descricao = "Descrição do curso", categoria, cargaHoraria = 10 }, Cancelamento);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)).GetProperty("id").GetInt32();
    }

    protected static async Task<int> IdDoPerfilAsync(HttpClient aluno) =>
        (await aluno.GetFromJsonAsync<JsonElement>("/tech-curse/Student/me", Cancelamento)).GetProperty("id").GetInt32();

    protected static async Task<int> MatricularAsync(HttpClient aluno, int cursoId)
    {
        var idDoAluno = await IdDoPerfilAsync(aluno);
        var resposta = await aluno.PostAsJsonAsync("/tech-curse/Enrollment", new { courseId = cursoId, studentId = idDoAluno }, Cancelamento);
        Assert.True(resposta.IsSuccessStatusCode, $"Matrícula falhou com {(int)resposta.StatusCode}");
        var matriculas = await aluno.GetFromJsonAsync<JsonElement>($"/tech-curse/Student/{idDoAluno}/enrollments", Cancelamento);
        return matriculas.EnumerateArray().First(m => m.GetProperty("courseId").GetInt32() == cursoId).GetProperty("enrollmentId").GetInt32();
    }

    protected static Task<HttpResponseMessage> EnviarComChaveAsync(HttpClient cliente, string rota, object corpo, string chave)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Post, rota) { Content = JsonContent.Create(corpo) };
        requisicao.Headers.Add("Idempotency-Key", chave);
        return cliente.SendAsync(requisicao, Cancelamento);
    }

    protected static async Task<int> CriarPagamentoAsync(HttpClient admin, int matriculaId, decimal valor = 100m)
    {
        var resposta = await EnviarComChaveAsync(admin, "/tech-curse/Payment", new { enrollmentId = matriculaId, amount = valor }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)).GetProperty("paymentId").GetInt32();
    }

    protected static async Task<string?> DetalheDoErroAsync(HttpResponseMessage resposta)
    {
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        return corpo.TryGetProperty("detail", out var detalhe) ? detalhe.GetString() : null;
    }
}
