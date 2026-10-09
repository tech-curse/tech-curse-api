using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TechCurse.Api.IntegrationTests.Infraestrutura;

namespace TechCurse.Api.IntegrationTests.Alunos;

public sealed class AlunosTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    [Fact]
    [Trait("Especificacao", "ALU-001")]
    public async Task Aluno_consulta_o_proprio_perfil()
    {
        var sessao = await RegistrarEEntrarAsync(nome: "Maria Souza");

        var perfil = await ClienteAutenticado(sessao.AccessToken).GetFromJsonAsync<JsonElement>("/tech-curse/Student/me", Cancelamento);

        Assert.Equal("Maria Souza", perfil.GetProperty("nome").GetString());
        Assert.Equal(sessao.Email, perfil.GetProperty("email").GetString());
        Assert.True(perfil.GetProperty("id").GetInt32() > 0);
        Assert.True(perfil.TryGetProperty("dataCadastro", out _));
    }

    [Theory]
    [Trait("Especificacao", "ALU-002")]
    [InlineData("Admin")]
    [InlineData("Instructor")]
    public async Task So_aluno_tem_me(string papel)
    {
        var cliente = ClienteAutenticado((await CriarUsuarioEEntrarAsync(papel)).AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/tech-curse/Student/me", Cancelamento)).StatusCode);
    }

    [Fact]
    [Trait("Especificacao", "ALU-003")]
    public async Task Aluno_sem_perfil_recebe_404()
    {
        var cliente = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Student")).AccessToken);

        var resposta = await cliente.GetAsync("/tech-curse/Student/me", Cancelamento);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal("Perfil de estudante não encontrado ou inativo.", await DetalheDoErroAsync(resposta));
    }

    [Fact]
    [Trait("Especificacao", "ALU-004")]
    public async Task Aluno_ve_o_proprio_perfil_por_id_e_Admin_ve_qualquer_um()
    {
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);
        var id = await IdDoPerfilAsync(aluno);
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);

        Assert.Equal(HttpStatusCode.OK, (await aluno.GetAsync($"/tech-curse/Student/{id}", Cancelamento)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/tech-curse/Student/{id}", Cancelamento)).StatusCode);
    }

    [Fact]
    [Trait("Especificacao", "ALU-005")]
    public async Task Aluno_nao_ve_o_perfil_de_outro_aluno()
    {
        var outro = await IdDoPerfilAsync(ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken));
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);

        var resposta = await aluno.GetAsync($"/tech-curse/Student/{outro}", Cancelamento);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal("Você não possui permissão suficiente para acessar este registro.", await DetalheDoErroAsync(resposta));
    }

    [Fact]
    [Trait("Especificacao", "ALU-006")]
    public async Task Perfil_inexistente_responde_404()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);

        var resposta = await admin.GetAsync("/tech-curse/Student/999999", Cancelamento);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal("Estudante não encontrado.", await DetalheDoErroAsync(resposta));
    }

    [Fact]
    [Trait("Especificacao", "ALU-007")]
    public async Task Admin_lista_os_alunos_sem_os_removidos()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var ativo = await IdDoPerfilAsync(ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken));
        var removido = await IdDoPerfilAsync(ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken));
        await admin.DeleteAsync($"/tech-curse/Student/{removido}", Cancelamento);

        var lista = await admin.GetFromJsonAsync<JsonElement>("/tech-curse/Student", Cancelamento);

        var ids = lista.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt32()).ToArray();
        Assert.Equal([ativo], ids);
        Assert.Equal(1, lista.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [Trait("Especificacao", "ALU-009")]
    [InlineData("Student")]
    [InlineData("Instructor")]
    public async Task So_Admin_lista_alunos(string papel)
    {
        var cliente = ClienteAutenticado((await CriarUsuarioEEntrarAsync(papel)).AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/tech-curse/Student", Cancelamento)).StatusCode);
    }

    [Fact]
    [Trait("Especificacao", "ALU-010")]
    public async Task Aluno_e_Admin_editam_o_nome_sem_mudar_email_nem_cadastro()
    {
        var sessao = await RegistrarEEntrarAsync(nome: "Nome Antigo");
        var aluno = ClienteAutenticado(sessao.AccessToken);
        var antes = await aluno.GetFromJsonAsync<JsonElement>("/tech-curse/Student/me", Cancelamento);
        var id = antes.GetProperty("id").GetInt32();
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);

        var peloAluno = await aluno.PutAsJsonAsync($"/tech-curse/Student/{id}", new { nome = "Nome do Aluno" }, Cancelamento);
        var peloAdmin = await admin.PutAsJsonAsync($"/tech-curse/Student/{id}", new { nome = "Nome do Admin" }, Cancelamento);

        Assert.Equal(HttpStatusCode.NoContent, peloAluno.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, peloAdmin.StatusCode);
        var depois = await aluno.GetFromJsonAsync<JsonElement>("/tech-curse/Student/me", Cancelamento);
        Assert.Equal("Nome do Admin", depois.GetProperty("nome").GetString());
        Assert.Equal(sessao.Email, depois.GetProperty("email").GetString());
        Assert.Equal(antes.GetProperty("dataCadastro").GetDateTime(), depois.GetProperty("dataCadastro").GetDateTime());
    }

    [Fact]
    [Trait("Especificacao", "ALU-011")]
    public async Task Aluno_nao_edita_o_perfil_de_outro()
    {
        var outro = ClienteAutenticado((await RegistrarEEntrarAsync(nome: "Intocado")).AccessToken);
        var idDoOutro = await IdDoPerfilAsync(outro);
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);

        var resposta = await aluno.PutAsJsonAsync($"/tech-curse/Student/{idDoOutro}", new { nome = "Invasor" }, Cancelamento);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal("Você não possui permissão suficiente para atualizar este registro.", await DetalheDoErroAsync(resposta));
        var perfil = await outro.GetFromJsonAsync<JsonElement>("/tech-curse/Student/me", Cancelamento);
        Assert.Equal("Intocado", perfil.GetProperty("nome").GetString());
    }

    [Theory]
    [Trait("Especificacao", "ALU-012")]
    [InlineData("", "O nome é obrigatório.")]
    [InlineData(null, "O nome deve ter no máximo 100 caracteres.")]
    public async Task Nome_invalido_e_recusado(string? nome, string mensagem)
    {
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);
        var id = await IdDoPerfilAsync(aluno);

        var resposta = await aluno.PutAsJsonAsync($"/tech-curse/Student/{id}", new { nome = nome ?? new string('a', 101) }, Cancelamento);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        var erros = (await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)).GetProperty("errors");
        Assert.Equal(mensagem, erros.GetProperty("Nome")[0].GetString());
    }

    [Fact]
    [Trait("Especificacao", "ALU-013")]
    [Trait("Especificacao", "ALU-015")]
    public async Task Editar_ou_remover_perfil_inexistente_ou_removido_responde_404()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var removido = await IdDoPerfilAsync(ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken));
        await admin.DeleteAsync($"/tech-curse/Student/{removido}", Cancelamento);

        foreach (var id in new[] { removido, 999999 })
        {
            var edicao = await admin.PutAsJsonAsync($"/tech-curse/Student/{id}", new { nome = "X" }, Cancelamento);
            var remocao = await admin.DeleteAsync($"/tech-curse/Student/{id}", Cancelamento);

            Assert.Equal(HttpStatusCode.NotFound, edicao.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, remocao.StatusCode);
            Assert.Equal("Estudante não encontrado.", await DetalheDoErroAsync(remocao));
        }
    }

    [Theory]
    [Trait("Especificacao", "ALU-017")]
    [InlineData("Student")]
    [InlineData("Instructor")]
    public async Task So_Admin_remove_aluno(string papel)
    {
        var alvo = await IdDoPerfilAsync(ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken));
        var cliente = ClienteAutenticado((await CriarUsuarioEEntrarAsync(papel)).AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.DeleteAsync($"/tech-curse/Student/{alvo}", Cancelamento)).StatusCode);
    }

    [Fact]
    [Trait("Especificacao", "ALU-018")]
    public async Task Historico_do_aluno_removido_continua_existindo()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);
        var idDoAluno = await IdDoPerfilAsync(aluno);
        var matriculaId = await MatricularAsync(aluno, await CriarCursoAsync(admin));
        var pagamentoId = await CriarPagamentoAsync(admin, matriculaId);

        await admin.DeleteAsync($"/tech-curse/Student/{idDoAluno}", Cancelamento);

        var lista = await admin.GetFromJsonAsync<JsonElement>("/tech-curse/Payment", Cancelamento);
        Assert.Contains(lista.GetProperty("items").EnumerateArray(), item => item.GetProperty("paymentId").GetInt32() == pagamentoId);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/tech-curse/Payment/{pagamentoId}", Cancelamento)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/tech-curse/Payment/enrollment/{matriculaId}", Cancelamento)).StatusCode);
    }

    [Fact]
    [Trait("Especificacao", "ALU-019")]
    public async Task Matriculas_do_aluno_para_ele_e_para_o_Admin_e_403_para_outro()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);
        var id = await IdDoPerfilAsync(aluno);
        var cursoId = await CriarCursoAsync(admin, "Curso Matriculado", "Frontend");
        var matriculaId = await MatricularAsync(aluno, cursoId);
        var outro = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);

        var proprias = await aluno.GetFromJsonAsync<JsonElement>($"/tech-curse/Student/{id}/enrollments", Cancelamento);
        var peloAdmin = await admin.GetAsync($"/tech-curse/Student/{id}/enrollments", Cancelamento);
        var peloOutro = await outro.GetAsync($"/tech-curse/Student/{id}/enrollments", Cancelamento);

        var matricula = proprias.EnumerateArray().Single();
        Assert.Equal(cursoId, matricula.GetProperty("courseId").GetInt32());
        Assert.Equal("Curso Matriculado", matricula.GetProperty("titulo").GetString());
        Assert.Equal("Frontend", matricula.GetProperty("categoria").GetString());
        Assert.True(matricula.GetProperty("matriculaAtiva").GetBoolean());
        Assert.Equal(matriculaId, matricula.GetProperty("enrollmentId").GetInt32());
        Assert.Equal(HttpStatusCode.OK, peloAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, peloOutro.StatusCode);
    }
}
