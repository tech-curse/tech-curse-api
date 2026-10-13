using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TechCurse.Api.IntegrationTests.Infraestrutura;
using TechCurse.Application.Interfaces;

namespace TechCurse.Api.IntegrationTests.Pagamentos;

public sealed class PagamentosTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    private const string PagamentoAtivoExistente = "Já existe um pagamento ativo para esta matrícula.";

    private sealed record Cenario(HttpClient Admin, HttpClient Aluno, int AlunoId, int MatriculaId);

    private async Task<Cenario> CenarioAsync()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);
        var matriculaId = await MatricularAsync(aluno, await CriarCursoAsync(admin));
        return new Cenario(admin, aluno, await IdDoPerfilAsync(aluno), matriculaId);
    }

    private static Task<HttpResponseMessage> ProcessarAsync(HttpClient cliente, int pagamentoId, string tipo = "CreditCard") =>
        EnviarComChaveAsync(cliente, "/tech-curse/Payment/process", new { paymentId = pagamentoId, type = tipo }, Guid.NewGuid().ToString());

    private static Task<HttpResponseMessage> EstornarAsync(HttpClient cliente, int pagamentoId) =>
        EnviarComChaveAsync(cliente, "/tech-curse/Payment/refund", new { paymentId = pagamentoId, reason = "Pedido do aluno" }, Guid.NewGuid().ToString());

    private static Task<JsonElement> PagamentoAsync(HttpClient cliente, int pagamentoId) =>
        cliente.GetFromJsonAsync<JsonElement>($"/tech-curse/Payment/{pagamentoId}", Cancelamento);

    private static int[] Ids(JsonElement lista) =>
        lista.EnumerateArray().Select(pagamento => pagamento.GetProperty("paymentId").GetInt32()).ToArray();

    private async Task<int> PagamentoEstornadoAsync(HttpClient admin, int matriculaId)
    {
        var id = await CriarPagamentoAsync(admin, matriculaId);
        Assert.Equal(HttpStatusCode.OK, (await ProcessarAsync(admin, id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await EstornarAsync(admin, id)).StatusCode);
        return id;
    }

    [Fact]
    [Trait("Especificacao", "PAG-001")]
    public async Task Admin_cria_pagamento_pendente_para_matricula_ativa()
    {
        var (admin, _, alunoId, matriculaId) = await CenarioAsync();

        var resposta = await EnviarComChaveAsync(admin, "/tech-curse/Payment", new { enrollmentId = matriculaId, amount = 150.50m }, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var pagamento = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        var id = pagamento.GetProperty("paymentId").GetInt32();
        Assert.Equal("Pending", pagamento.GetProperty("status").GetString());
        Assert.True(pagamento.GetProperty("isActive").GetBoolean());
        Assert.Equal(alunoId, pagamento.GetProperty("studentId").GetInt32());
        Assert.Equal(150.50m, pagamento.GetProperty("amount").GetDecimal());
        Assert.Equal("Curso de teste", pagamento.GetProperty("courseTitulo").GetString());
        Assert.EndsWith($"/tech-curse/Payment/{id}", resposta.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Especificacao", "PAG-002")]
    public async Task Matricula_inexistente_responde_404()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);

        var resposta = await EnviarComChaveAsync(admin, "/tech-curse/Payment", new { enrollmentId = 999999, amount = 10m }, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal("Matrícula não encontrada.", await DetalheDoErroAsync(resposta));
    }

    [Fact]
    [Trait("Especificacao", "PAG-003")]
    public async Task Matricula_aceita_um_pagamento_ativo_pendente_ou_pago()
    {
        var (admin, _, _, matriculaId) = await CenarioAsync();
        var id = await CriarPagamentoAsync(admin, matriculaId);
        var comPendente = await EnviarComChaveAsync(admin, "/tech-curse/Payment", new { enrollmentId = matriculaId, amount = 10m }, Guid.NewGuid().ToString());
        await ProcessarAsync(admin, id);

        var comPago = await EnviarComChaveAsync(admin, "/tech-curse/Payment", new { enrollmentId = matriculaId, amount = 10m }, Guid.NewGuid().ToString());

        foreach (var resposta in new[] { comPendente, comPago })
        {
            Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
            Assert.Equal(PagamentoAtivoExistente, await DetalheDoErroAsync(resposta));
        }
    }

    [Fact]
    [Trait("Especificacao", "PAG-004")]
    public async Task Criacoes_simultaneas_nao_geram_dois_pagamentos_ativos()
    {
        var (admin, _, _, matriculaId) = await CenarioAsync();
        var token = admin.DefaultRequestHeaders.Authorization!.Parameter!;
        var clientes = Enumerable.Range(0, 10).Select(_ => ClienteAutenticado(token)).ToArray();

        var respostas = await Task.WhenAll(clientes.Select(cliente =>
            EnviarComChaveAsync(cliente, "/tech-curse/Payment", new { enrollmentId = matriculaId, amount = 10m }, Guid.NewGuid().ToString())));

        Assert.Equal(1, respostas.Count(resposta => resposta.StatusCode == HttpStatusCode.Created));
        foreach (var resposta in respostas.Where(resposta => resposta.StatusCode != HttpStatusCode.Created))
        {
            Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
            Assert.Equal(PagamentoAtivoExistente, await DetalheDoErroAsync(resposta));
        }

        Assert.Single((await admin.GetFromJsonAsync<JsonElement>($"/tech-curse/Payment/enrollment/{matriculaId}", Cancelamento)).EnumerateArray());
    }

    [Theory]
    [Trait("Especificacao", "PAG-005")]
    [InlineData(0, 10, "EnrollmentId", "O ID da matrícula deve ser maior que zero.")]
    [InlineData(1, 0, "Amount", "O valor do pagamento deve ser maior que zero.")]
    [InlineData(1, -5, "Amount", "O valor do pagamento deve ser maior que zero.")]
    public async Task Dados_invalidos_sao_recusados(int matriculaId, decimal valor, string campo, string mensagem)
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);

        var resposta = await EnviarComChaveAsync(admin, "/tech-curse/Payment", new { enrollmentId = matriculaId, amount = valor }, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        var erros = (await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)).GetProperty("errors");
        Assert.Equal(mensagem, erros.GetProperty(campo)[0].GetString());
    }

    [Theory]
    [Trait("Especificacao", "PAG-006")]
    [InlineData("Student")]
    [InlineData("Instructor")]
    public async Task So_Admin_cria_processa_e_estorna(string papel)
    {
        var (admin, _, _, matriculaId) = await CenarioAsync();
        var id = await CriarPagamentoAsync(admin, matriculaId);
        var cliente = ClienteAutenticado((await CriarUsuarioEEntrarAsync(papel)).AccessToken);

        HttpResponseMessage[] respostas =
        [
            await EnviarComChaveAsync(cliente, "/tech-curse/Payment", new { enrollmentId = matriculaId, amount = 10m }, Guid.NewGuid().ToString()),
            await ProcessarAsync(cliente, id),
            await EstornarAsync(cliente, id)
        ];

        Assert.All(respostas, resposta => Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode));
        Assert.Equal("Pending", (await PagamentoAsync(admin, id)).GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Especificacao", "PAG-007")]
    public async Task Cartao_de_credito_aprova_o_pagamento()
    {
        var (admin, _, _, matriculaId) = await CenarioAsync();
        var id = await CriarPagamentoAsync(admin, matriculaId);

        var resposta = await ProcessarAsync(admin, id);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        Assert.True(corpo.GetProperty("success").GetBoolean());
        Assert.Equal("Pagamento processado com sucesso.", corpo.GetProperty("message").GetString());
        var transacao = corpo.GetProperty("externalTransactionId").GetString();
        Assert.StartsWith("sim_", transacao);
        var pagamento = await PagamentoAsync(admin, id);
        Assert.Equal("Paid", pagamento.GetProperty("status").GetString());
        Assert.Equal(transacao, pagamento.GetProperty("externalTransactionId").GetString());
        Assert.Equal(DateTimeKind.Utc, pagamento.GetProperty("paidAt").GetDateTime().Kind);
    }

    [Theory]
    [Trait("Especificacao", "PAG-009")]
    [InlineData("999.99", "Falha ao processar pagamento [FRAUD_DETECTED]: Transação recusada pelo sistema antifraude.")]
    [InlineData("500.00", "Falha ao processar pagamento [INSUFFICIENT_FUNDS]: Saldo insuficiente no método de pagamento.")]
    public async Task Recusa_do_gateway_mantem_o_pagamento_pendente(string valor, string mensagem)
    {
        var (admin, _, _, matriculaId) = await CenarioAsync();
        var id = await CriarPagamentoAsync(admin, matriculaId, decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture));

        var primeira = await ProcessarAsync(admin, id);
        var segunda = await ProcessarAsync(admin, id);

        Assert.Equal(HttpStatusCode.BadRequest, primeira.StatusCode);
        Assert.Equal(mensagem, await DetalheDoErroAsync(primeira));
        Assert.Equal(HttpStatusCode.BadRequest, segunda.StatusCode);
        var pagamento = await PagamentoAsync(admin, id);
        Assert.Equal("Pending", pagamento.GetProperty("status").GetString());
        Assert.True(pagamento.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    [Trait("Especificacao", "PAG-010")]
    public async Task Processar_ou_estornar_pagamento_inexistente_responde_404()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);

        foreach (var resposta in new[] { await ProcessarAsync(admin, 999999), await EstornarAsync(admin, 999999) })
        {
            Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
            Assert.Equal("Pagamento não encontrado.", await DetalheDoErroAsync(resposta));
        }
    }

    [Theory]
    [Trait("Especificacao", "PAG-011")]
    [InlineData("Pix")]
    [InlineData("Boleto")]
    public async Task Metodo_sem_suporte_e_recusado_com_422(string tipo)
    {
        var (admin, _, _, matriculaId) = await CenarioAsync();
        var id = await CriarPagamentoAsync(admin, matriculaId);

        var resposta = await ProcessarAsync(admin, id, tipo);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        var erros = (await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)).GetProperty("errors");
        Assert.Equal(["Método de pagamento não suportado."], erros.GetProperty("Type").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("Pending", (await PagamentoAsync(admin, id)).GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Especificacao", "PAG-012")]
    public async Task Gateway_sem_resposta_gera_504_e_nada_muda()
    {
        var (admin, _, _, matriculaId) = await CenarioAsync();
        var pendente = await CriarPagamentoAsync(admin, matriculaId);
        await using var fabrica = Ambiente.Fabrica.WithWebHostBuilder(builder => builder.ConfigureTestServices(servicos =>
            servicos.AddScoped<IPaymentGatewayAdapter, GatewayQueEsgotaOTempo>()));
        using var lento = fabrica.CreateClient();
        lento.DefaultRequestHeaders.Authorization = admin.DefaultRequestHeaders.Authorization;

        var processamento = await ProcessarAsync(lento, pendente);
        Assert.Equal(HttpStatusCode.OK, (await ProcessarAsync(admin, pendente)).StatusCode);
        var estorno = await EstornarAsync(lento, pendente);

        Assert.Equal(HttpStatusCode.GatewayTimeout, processamento.StatusCode);
        Assert.Equal(HttpStatusCode.GatewayTimeout, estorno.StatusCode);
        var pagamento = await PagamentoAsync(admin, pendente);
        Assert.Equal("Paid", pagamento.GetProperty("status").GetString());
        Assert.True(pagamento.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    [Trait("Especificacao", "PAG-014")]
    public async Task Admin_estorna_pagamento_pago_e_libera_a_matricula()
    {
        var (admin, _, _, matriculaId) = await CenarioAsync();
        var id = await CriarPagamentoAsync(admin, matriculaId);
        await ProcessarAsync(admin, id);

        var resposta = await EstornarAsync(admin, id);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        Assert.True(corpo.GetProperty("success").GetBoolean());
        Assert.Equal("Estorno realizado com sucesso.", corpo.GetProperty("message").GetString());
        var pagamento = await PagamentoAsync(admin, id);
        Assert.Equal("Refunded", pagamento.GetProperty("status").GetString());
        Assert.False(pagamento.GetProperty("isActive").GetBoolean());
        await CriarPagamentoAsync(admin, matriculaId);
    }

    [Fact]
    [Trait("Especificacao", "PAG-015")]
    public async Task So_pagamento_pago_e_estornado()
    {
        var (admin, _, _, matriculaId) = await CenarioAsync();
        var estornado = await PagamentoEstornadoAsync(admin, matriculaId);
        var pendente = await CriarPagamentoAsync(admin, matriculaId);

        foreach (var id in new[] { pendente, estornado })
        {
            var resposta = await EstornarAsync(admin, id);

            Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
            Assert.Equal("Apenas pagamentos processados e com ID de transação podem ser estornados.", await DetalheDoErroAsync(resposta));
        }
    }

    [Fact]
    [Trait("Especificacao", "PAG-017")]
    public async Task Admin_lista_todos_os_pagamentos_inclusive_de_aluno_removido()
    {
        var (admin, _, alunoId, matriculaId) = await CenarioAsync();
        var primeiro = await CriarPagamentoAsync(admin, matriculaId);
        var outro = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);
        var segundo = await CriarPagamentoAsync(admin, await MatricularAsync(outro, await CriarCursoAsync(admin, "Outro")));
        await admin.DeleteAsync($"/tech-curse/Student/{alunoId}", Cancelamento);

        var pagina = await admin.GetFromJsonAsync<JsonElement>("/tech-curse/Payment", Cancelamento);

        Assert.Equal([segundo, primeiro], Ids(pagina.GetProperty("items")));
        foreach (var papel in new[] { "Student", "Instructor" })
        {
            var cliente = ClienteAutenticado((await CriarUsuarioEEntrarAsync(papel)).AccessToken);
            Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/tech-curse/Payment", Cancelamento)).StatusCode);
        }
    }

    [Fact]
    [Trait("Especificacao", "PAG-018")]
    public async Task Aluno_e_Admin_consultam_os_pagamentos_do_aluno()
    {
        var (admin, aluno, alunoId, matriculaId) = await CenarioAsync();
        var primeiro = await CriarPagamentoAsync(admin, matriculaId);
        var segundo = await CriarPagamentoAsync(admin, await MatricularAsync(aluno, await CriarCursoAsync(admin, "Outro")));
        var outro = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);
        await CriarPagamentoAsync(admin, await MatricularAsync(outro, await CriarCursoAsync(admin, "Terceiro")));

        var doAluno = await aluno.GetFromJsonAsync<JsonElement>($"/tech-curse/Payment/student/{alunoId}", Cancelamento);
        var peloAdmin = await admin.GetFromJsonAsync<JsonElement>($"/tech-curse/Payment/student/{alunoId}", Cancelamento);

        Assert.Equal([segundo, primeiro], Ids(doAluno.GetProperty("items")));
        Assert.Equal(2, doAluno.GetProperty("totalCount").GetInt32());
        Assert.Equal([segundo, primeiro], Ids(peloAdmin.GetProperty("items")));
    }

    [Fact]
    [Trait("Especificacao", "PAG-020")]
    public async Task Admin_ve_os_pagamentos_de_aluno_removido()
    {
        var (admin, _, alunoId, matriculaId) = await CenarioAsync();
        var id = await CriarPagamentoAsync(admin, matriculaId);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/tech-curse/Student/{alunoId}", Cancelamento)).StatusCode);

        var porAluno = await admin.GetAsync($"/tech-curse/Payment/student/{alunoId}", Cancelamento);
        var porId = await admin.GetAsync($"/tech-curse/Payment/{id}", Cancelamento);
        var porMatricula = await admin.GetAsync($"/tech-curse/Payment/enrollment/{matriculaId}", Cancelamento);

        Assert.Equal(HttpStatusCode.OK, porAluno.StatusCode);
        Assert.Equal([id], Ids((await porAluno.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)).GetProperty("items")));
        Assert.Equal(HttpStatusCode.OK, porId.StatusCode);
        Assert.Equal(HttpStatusCode.OK, porMatricula.StatusCode);
        Assert.Equal([id], Ids(await porMatricula.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)));
    }

    [Fact]
    [Trait("Especificacao", "PAG-021")]
    public async Task Pagamento_por_id_para_o_dono_e_o_Admin()
    {
        var (admin, aluno, _, matriculaId) = await CenarioAsync();
        var id = await CriarPagamentoAsync(admin, matriculaId);

        Assert.Equal(id, (await PagamentoAsync(aluno, id)).GetProperty("paymentId").GetInt32());
        Assert.Equal(id, (await PagamentoAsync(admin, id)).GetProperty("paymentId").GetInt32());
        var inexistente = await admin.GetAsync("/tech-curse/Payment/999999", Cancelamento);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.Equal("Pagamento não encontrado.", await DetalheDoErroAsync(inexistente));
    }

    [Fact]
    [Trait("Especificacao", "PAG-022")]
    public async Task Pagamentos_de_uma_matricula()
    {
        var (admin, aluno, _, matriculaId) = await CenarioAsync();
        var semPagamentos = await MatricularAsync(aluno, await CriarCursoAsync(admin, "Sem pagamentos"));
        var vazia = await aluno.GetAsync($"/tech-curse/Payment/enrollment/{semPagamentos}", Cancelamento);
        var estornado = await PagamentoEstornadoAsync(admin, matriculaId);
        var atual = await CriarPagamentoAsync(admin, matriculaId);

        var doAluno = await aluno.GetFromJsonAsync<JsonElement>($"/tech-curse/Payment/enrollment/{matriculaId}", Cancelamento);
        var peloAdmin = await admin.GetFromJsonAsync<JsonElement>($"/tech-curse/Payment/enrollment/{matriculaId}", Cancelamento);
        var inexistente = await admin.GetAsync("/tech-curse/Payment/enrollment/999999", Cancelamento);

        Assert.Equal([atual, estornado], Ids(doAluno));
        Assert.Equal([atual, estornado], Ids(peloAdmin));
        Assert.Equal(HttpStatusCode.OK, vazia.StatusCode);
        Assert.Empty((await vazia.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.Equal("Matrícula não encontrada.", await DetalheDoErroAsync(inexistente));
    }
}
