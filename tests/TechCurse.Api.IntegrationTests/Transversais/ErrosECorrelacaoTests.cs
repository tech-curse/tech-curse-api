using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TechCurse.Api.IntegrationTests.Infraestrutura;
using TechCurse.Api.Middleware;
using TechCurse.Application.Interfaces;

namespace TechCurse.Api.IntegrationTests.Transversais;

public sealed class ErrosECorrelacaoTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    [Fact]
    [Trait("Especificacao", "TRV-001")]
    public async Task Erro_de_dominio_sai_como_ProblemDetails_com_o_status_mapeado()
    {
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);

        var resposta = await aluno.GetAsync("/tech-curse/Course/999999", Cancelamento);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        Assert.Equal("NotFound", corpo.GetProperty("title").GetString());
        Assert.Equal(404, corpo.GetProperty("status").GetInt32());
        Assert.Equal("Curso não encontrado.", corpo.GetProperty("detail").GetString());
        Assert.Equal("/tech-curse/Course/999999", corpo.GetProperty("instance").GetString());
    }

    [Fact]
    [Trait("Especificacao", "TRV-002")]
    public async Task Erro_de_validacao_traz_os_campos_com_problema()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);

        var resposta = await admin.PostAsJsonAsync("/tech-curse/Course",
            new { titulo = "", descricao = "Descrição", categoria = "Backend", cargaHoraria = 0 }, Cancelamento);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        Assert.Equal("Ocorreram um ou mais erros de validação.", corpo.GetProperty("detail").GetString());
        Assert.Equal("O título é obrigatório.", corpo.GetProperty("errors").GetProperty("Titulo")[0].GetString());
        Assert.Equal("A carga horária deve ser maior que zero.", corpo.GetProperty("errors").GetProperty("CargaHoraria")[0].GetString());
    }

    [Fact]
    [Trait("Especificacao", "TRV-003")]
    public async Task Corpo_malformado_responde_400_no_formato_do_ASP_NET_Core()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);

        var resposta = await admin.PostAsync("/tech-curse/Course",
            new StringContent("{ isto não é json", Encoding.UTF8, "application/json"), Cancelamento);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        Assert.Equal("One or more validation errors occurred.", corpo.GetProperty("title").GetString());
        Assert.True(corpo.TryGetProperty("traceId", out _));
    }

    [Fact]
    [Trait("Especificacao", "TRV-004")]
    public async Task Erro_inesperado_nao_vaza_detalhes_internos()
    {
        await using var fabrica = Ambiente.Fabrica.WithWebHostBuilder(builder => builder.ConfigureTestServices(servicos =>
            servicos.AddScoped<ICourseRepository, RepositorioDeCursosQueFalha>()));
        using var cliente = fabrica.CreateClient();
        var sessao = await RegistrarEEntrarAsync();
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, "/tech-curse/Course");
        requisicao.Headers.Authorization = new("Bearer", sessao.AccessToken);

        var resposta = await cliente.SendAsync(requisicao, Cancelamento);

        Assert.Equal(HttpStatusCode.InternalServerError, resposta.StatusCode);
        var texto = await resposta.Content.ReadAsStringAsync(Cancelamento);
        Assert.DoesNotContain("servidor-secreto", texto);
        Assert.Equal("Ocorreu um erro inesperado. Informe o código de correlação ao suporte.",
            JsonDocument.Parse(texto).RootElement.GetProperty("detail").GetString());
        Assert.True(resposta.Headers.Contains("X-Correlation-ID"));
    }

    [Fact]
    [Trait("Especificacao", "TRV-005")]
    public async Task Toda_resposta_tem_identificador_de_correlacao()
    {
        var semCabecalho = await Cliente.GetAsync("/health/live", Cancelamento);
        using var comCabecalho = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        comCabecalho.Headers.Add("X-Correlation-ID", "minha-correlacao-123");
        var ecoada = await Cliente.SendAsync(comCabecalho, Cancelamento);

        Assert.True(Guid.TryParse(semCabecalho.Headers.GetValues("X-Correlation-ID").Single(), out _));
        Assert.Equal("minha-correlacao-123", ecoada.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Theory]
    [Trait("Especificacao", "TRV-006")]
    [InlineData("abc def")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("valor;outro")]
    [InlineData("sessao_123")]
    public async Task Identificador_de_correlacao_com_caractere_invalido_e_trocado(string recebido)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        requisicao.Headers.TryAddWithoutValidation("X-Correlation-ID", recebido);

        var resposta = await Cliente.SendAsync(requisicao, Cancelamento);

        Assert.True(Guid.TryParse(resposta.Headers.GetValues("X-Correlation-ID").Single(), out _));
    }

    [Fact]
    [Trait("Especificacao", "TRV-006")]
    public async Task Identificador_de_correlacao_vale_ate_64_caracteres()
    {
        var limite = new string('a', CorrelationIdMiddleware.TamanhoMaximo);
        using var noLimite = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        noLimite.Headers.Add("X-Correlation-ID", limite);
        using var acima = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        acima.Headers.Add("X-Correlation-ID", limite + "a");

        var aceita = await Cliente.SendAsync(noLimite, Cancelamento);
        var trocada = await Cliente.SendAsync(acima, Cancelamento);

        Assert.Equal(limite, aceita.Headers.GetValues("X-Correlation-ID").Single());
        Assert.True(Guid.TryParse(trocada.Headers.GetValues("X-Correlation-ID").Single(), out _));
    }

    [Fact]
    [Trait("Especificacao", "TRV-040")]
    public async Task Recusa_esperada_e_registrada_como_aviso_sem_a_pilha()
    {
        var logger = new LoggerQueGuarda<ExceptionHandlingMiddleware>();
        await using var fabrica = Ambiente.Fabrica.WithWebHostBuilder(builder => builder.ConfigureTestServices(servicos =>
            servicos.AddSingleton<ILogger<ExceptionHandlingMiddleware>>(logger)));
        using var cliente = fabrica.CreateClient();
        var sessao = await RegistrarEEntrarAsync();
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, "/tech-curse/Course/999999");
        requisicao.Headers.Authorization = new("Bearer", sessao.AccessToken);

        Assert.Equal(HttpStatusCode.NotFound, (await cliente.SendAsync(requisicao, Cancelamento)).StatusCode);

        var evento = Assert.Single(logger.Eventos);
        Assert.Equal(LogLevel.Warning, evento.Nivel);
        Assert.Null(evento.Excecao);
        Assert.Contains("404", evento.Mensagem);
        Assert.Contains("Curso não encontrado.", evento.Mensagem);
    }

    [Fact]
    [Trait("Especificacao", "TRV-040")]
    public async Task Falha_inesperada_e_registrada_como_erro_com_a_excecao()
    {
        var logger = new LoggerQueGuarda<ExceptionHandlingMiddleware>();
        await using var fabrica = Ambiente.Fabrica.WithWebHostBuilder(builder => builder.ConfigureTestServices(servicos =>
        {
            servicos.AddScoped<ICourseRepository, RepositorioDeCursosQueFalha>();
            servicos.AddSingleton<ILogger<ExceptionHandlingMiddleware>>(logger);
        }));
        using var cliente = fabrica.CreateClient();
        var sessao = await RegistrarEEntrarAsync();
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, "/tech-curse/Course");
        requisicao.Headers.Authorization = new("Bearer", sessao.AccessToken);

        Assert.Equal(HttpStatusCode.InternalServerError, (await cliente.SendAsync(requisicao, Cancelamento)).StatusCode);

        var evento = Assert.Single(logger.Eventos);
        Assert.Equal(LogLevel.Error, evento.Nivel);
        Assert.NotNull(evento.Excecao);
        Assert.Contains("500", evento.Mensagem);
    }

    [Theory]
    [Trait("Especificacao", "TRV-023")]
    [InlineData("ConnectionStrings:APITechCurse", "APITechCurse")]
    [InlineData("ConnectionStrings:RedisCache", "RedisCache")]
    public async Task Sem_configuracao_obrigatoria_a_API_nao_sobe(string chave, string trechoDaMensagem)
    {
        await using var fabrica = Ambiente.Fabrica.WithWebHostBuilder(builder => builder.UseSetting(chave, ""));

        var erro = Record.Exception(() => fabrica.CreateClient());

        Assert.NotNull(erro);
        var raiz = erro;
        while (raiz.InnerException is not null)
        {
            raiz = raiz.InnerException;
        }

        Assert.Contains(trechoDaMensagem, raiz.Message);
    }
}
