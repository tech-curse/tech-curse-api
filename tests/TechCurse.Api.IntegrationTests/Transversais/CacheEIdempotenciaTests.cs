using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TechCurse.Api.IntegrationTests.Infraestrutura;

namespace TechCurse.Api.IntegrationTests.Transversais;

public sealed class CacheEIdempotenciaTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    [Fact]
    [Trait("Especificacao", "TRV-019")]
    [Trait("Especificacao", "CUR-018")]
    public async Task Curso_editado_pelo_Admin_aparece_na_hora_para_o_aluno()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);
        var cursoId = await CriarCursoAsync(admin, "Título antigo");
        await aluno.GetAsync("/tech-curse/Course", Cancelamento);
        await aluno.GetAsync($"/tech-curse/Course/{cursoId}", Cancelamento);

        var edicao = await admin.PutAsJsonAsync($"/tech-curse/Course/{cursoId}",
            new { titulo = "Título novo", descricao = "Descrição do curso", categoria = "Backend", cargaHoraria = 10 }, Cancelamento);
        Assert.Equal(HttpStatusCode.NoContent, edicao.StatusCode);

        var detalhe = await aluno.GetFromJsonAsync<JsonElement>($"/tech-curse/Course/{cursoId}", Cancelamento);
        var lista = await aluno.GetFromJsonAsync<JsonElement>("/tech-curse/Course", Cancelamento);
        Assert.Equal("Título novo", detalhe.GetProperty("titulo").GetString());
        Assert.Equal("Título novo", lista.GetProperty("items")[0].GetProperty("titulo").GetString());
    }

    [Fact]
    [Trait("Especificacao", "TRV-019")]
    [Trait("Especificacao", "CUR-018")]
    public async Task Curso_removido_pelo_Admin_some_na_hora_para_o_aluno()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);
        var cursoId = await CriarCursoAsync(admin);
        await aluno.GetAsync($"/tech-curse/Course/{cursoId}", Cancelamento);
        await aluno.GetAsync("/tech-curse/Course", Cancelamento);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/tech-curse/Course/{cursoId}", Cancelamento)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await aluno.GetAsync($"/tech-curse/Course/{cursoId}", Cancelamento)).StatusCode);
        var lista = await aluno.GetFromJsonAsync<JsonElement>("/tech-curse/Course", Cancelamento);
        Assert.Equal(0, lista.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    [Trait("Especificacao", "TRV-019")]
    [Trait("Especificacao", "PAG-023")]
    public async Task Pagamento_criado_pelo_Admin_aparece_na_hora_para_o_aluno()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);
        var matriculaId = await MatricularAsync(aluno, await CriarCursoAsync(admin));
        var idDoAluno = await IdDoPerfilAsync(aluno);
        var antes = await aluno.GetFromJsonAsync<JsonElement>($"/tech-curse/Payment/student/{idDoAluno}", Cancelamento);
        Assert.Equal(0, antes.GetProperty("totalCount").GetInt32());

        await CriarPagamentoAsync(admin, matriculaId);

        var depois = await aluno.GetFromJsonAsync<JsonElement>($"/tech-curse/Payment/student/{idDoAluno}", Cancelamento);
        Assert.Equal(1, depois.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    [Trait("Especificacao", "TRV-014")]
    public async Task Escrita_idempotente_sem_chave_responde_400()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);

        var resposta = await admin.PostAsJsonAsync("/tech-curse/Payment", new { enrollmentId = 1, amount = 10m }, Cancelamento);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal("O header 'Idempotency-Key' é obrigatório para requisições idempotentes.", await DetalheDoErroAsync(resposta));
    }

    [Fact]
    [Trait("Especificacao", "TRV-015")]
    public async Task Mesma_chave_devolve_a_mesma_resposta_sem_executar_de_novo()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);
        var matriculaId = await MatricularAsync(aluno, await CriarCursoAsync(admin));
        var corpo = new { enrollmentId = matriculaId, amount = 100m };
        var chave = Guid.NewGuid().ToString();

        var primeira = await EnviarComChaveAsync(admin, "/tech-curse/Payment", corpo, chave);
        var repeticao = await EnviarComChaveAsync(admin, "/tech-curse/Payment", corpo, chave);

        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.Created, repeticao.StatusCode);
        Assert.Equal(await primeira.Content.ReadAsStringAsync(Cancelamento), await repeticao.Content.ReadAsStringAsync(Cancelamento));
        var pagamentos = await admin.GetFromJsonAsync<JsonElement>($"/tech-curse/Payment/enrollment/{matriculaId}", Cancelamento);
        Assert.Equal(1, pagamentos.GetArrayLength());
    }

    [Fact]
    [Trait("Especificacao", "TRV-016")]
    public async Task Mesma_chave_em_outro_endpoint_executa_normalmente()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);
        var matriculaId = await MatricularAsync(aluno, await CriarCursoAsync(admin));
        var chave = Guid.NewGuid().ToString();
        var criacao = await EnviarComChaveAsync(admin, "/tech-curse/Payment", new { enrollmentId = matriculaId, amount = 100m }, chave);
        var pagamentoId = (await criacao.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)).GetProperty("paymentId").GetInt32();

        var processamento = await EnviarComChaveAsync(admin, "/tech-curse/Payment/process", new { paymentId = pagamentoId, type = "CreditCard" }, chave);

        Assert.Equal(HttpStatusCode.OK, processamento.StatusCode);
        var corpo = await processamento.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        Assert.True(corpo.GetProperty("success").GetBoolean());
    }

    [Fact]
    [Trait("Especificacao", "TRV-017")]
    public async Task Erro_nao_e_guardado_e_a_repeticao_executa_de_novo()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);
        var matriculaId = await MatricularAsync(aluno, await CriarCursoAsync(admin));
        var pagamentoId = await CriarPagamentoAsync(admin, matriculaId);
        await EnviarComChaveAsync(admin, "/tech-curse/Payment/process", new { paymentId = pagamentoId, type = "CreditCard" }, Guid.NewGuid().ToString());
        var corpo = new { enrollmentId = matriculaId, amount = 50m };
        var chave = Guid.NewGuid().ToString();

        var recusada = await EnviarComChaveAsync(admin, "/tech-curse/Payment", corpo, chave);
        await EnviarComChaveAsync(admin, "/tech-curse/Payment/refund", new { paymentId = pagamentoId, reason = "teste" }, Guid.NewGuid().ToString());
        var repetida = await EnviarComChaveAsync(admin, "/tech-curse/Payment", corpo, chave);

        Assert.Equal(HttpStatusCode.Conflict, recusada.StatusCode);
        Assert.Equal(HttpStatusCode.Created, repetida.StatusCode);
    }
}
