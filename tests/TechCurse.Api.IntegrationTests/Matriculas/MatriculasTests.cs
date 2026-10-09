using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TechCurse.Api.IntegrationTests.Infraestrutura;

namespace TechCurse.Api.IntegrationTests.Matriculas;

public sealed class MatriculasTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    private static Task<HttpResponseMessage> MatricularComAsync(HttpClient cliente, int cursoId, int alunoId) =>
        cliente.PostAsJsonAsync("/tech-curse/Enrollment", new { courseId = cursoId, studentId = alunoId }, Cancelamento);

    private static async Task<JsonElement[]> MatriculasDeAsync(HttpClient cliente, int alunoId) =>
        (await cliente.GetFromJsonAsync<JsonElement>($"/tech-curse/Student/{alunoId}/enrollments", Cancelamento)).EnumerateArray().ToArray();

    [Fact]
    [Trait("Especificacao", "MAT-001")]
    public async Task Aluno_se_matricula_num_curso()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);
        var alunoId = await IdDoPerfilAsync(aluno);
        var cursoId = await CriarCursoAsync(admin);

        var resposta = await MatricularComAsync(aluno, cursoId, alunoId);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        Assert.Equal("Aluno matriculado com sucesso.", corpo.GetProperty("mensagem").GetString());
        var matricula = (await MatriculasDeAsync(aluno, alunoId)).Single();
        Assert.Equal(cursoId, matricula.GetProperty("courseId").GetInt32());
        Assert.True(matricula.GetProperty("matriculaAtiva").GetBoolean());
    }

    [Fact]
    [Trait("Especificacao", "MAT-002")]
    public async Task Aluno_so_matricula_a_si_mesmo()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var outro = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);
        var outroId = await IdDoPerfilAsync(outro);
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);
        var alunoId = await IdDoPerfilAsync(aluno);
        var cursoId = await CriarCursoAsync(admin);

        await MatricularComAsync(aluno, cursoId, outroId);

        Assert.Single(await MatriculasDeAsync(aluno, alunoId));
        Assert.Empty(await MatriculasDeAsync(outro, outroId));
    }

    [Fact]
    [Trait("Especificacao", "MAT-003")]
    public async Task Admin_matricula_um_aluno()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);
        var alunoId = await IdDoPerfilAsync(aluno);
        var cursoId = await CriarCursoAsync(admin);

        var resposta = await MatricularComAsync(admin, cursoId, alunoId);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Equal(cursoId, (await MatriculasDeAsync(aluno, alunoId)).Single().GetProperty("courseId").GetInt32());
    }

    [Fact]
    [Trait("Especificacao", "MAT-005")]
    public async Task Matricula_duplicada_e_recusada()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);
        var alunoId = await IdDoPerfilAsync(aluno);
        var cursoId = await CriarCursoAsync(admin);
        await MatricularComAsync(aluno, cursoId, alunoId);

        var repetida = await MatricularComAsync(aluno, cursoId, alunoId);

        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
        Assert.Equal("Estudante já está matriculado neste curso!", await DetalheDoErroAsync(repetida));
        Assert.Single(await MatriculasDeAsync(aluno, alunoId));
    }

    [Fact]
    [Trait("Especificacao", "MAT-006")]
    public async Task Requisicoes_simultaneas_nao_criam_duas_matriculas()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var sessao = await RegistrarEEntrarAsync();
        var alunoId = await IdDoPerfilAsync(ClienteAutenticado(sessao.AccessToken));
        var cursoId = await CriarCursoAsync(admin);
        var clientes = Enumerable.Range(0, 10).Select(_ => ClienteAutenticado(sessao.AccessToken)).ToArray();

        var respostas = await Task.WhenAll(clientes.Select(cliente => MatricularComAsync(cliente, cursoId, alunoId)));

        Assert.Equal(1, respostas.Count(resposta => resposta.StatusCode == HttpStatusCode.Created));
        Assert.All(respostas.Where(resposta => resposta.StatusCode != HttpStatusCode.Created),
            resposta => Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode));
        Assert.Single(await MatriculasDeAsync(clientes[0], alunoId));
    }

    [Fact]
    [Trait("Especificacao", "MAT-007")]
    public async Task Curso_inexistente_responde_404()
    {
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);

        var resposta = await MatricularComAsync(aluno, 999999, await IdDoPerfilAsync(aluno));

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal("Curso não encontrado!", await DetalheDoErroAsync(resposta));
    }

    [Fact]
    [Trait("Especificacao", "MAT-008")]
    public async Task Aluno_inexistente_removido_ou_sem_perfil_responde_404()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var cursoId = await CriarCursoAsync(admin);
        var removido = await IdDoPerfilAsync(ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken));
        await admin.DeleteAsync($"/tech-curse/Student/{removido}", Cancelamento);
        var semPerfil = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Student")).AccessToken);

        HttpResponseMessage[] respostas =
        [
            await MatricularComAsync(admin, cursoId, 999999),
            await MatricularComAsync(admin, cursoId, removido),
            await MatricularComAsync(semPerfil, cursoId, 1)
        ];

        foreach (var resposta in respostas)
        {
            Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
            Assert.Equal("Estudante não encontrado!", await DetalheDoErroAsync(resposta));
        }
    }

    [Theory]
    [Trait("Especificacao", "MAT-009")]
    [InlineData(0, 1, "CourseId", "O ID do curso deve ser maior que zero.")]
    [InlineData(1, -1, "StudentId", "O ID do estudante deve ser maior que zero.")]
    public async Task Ids_invalidos_sao_recusados(int cursoId, int alunoId, string campo, string mensagem)
    {
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);

        var resposta = await MatricularComAsync(aluno, cursoId, alunoId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        var erros = (await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)).GetProperty("errors");
        Assert.Equal(mensagem, erros.GetProperty(campo)[0].GetString());
    }

    [Fact]
    [Trait("Especificacao", "MAT-010")]
    [Trait("Especificacao", "CUR-015")]
    public async Task Curso_com_matricula_nao_pode_ser_removido()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);
        var cursoId = await CriarCursoAsync(admin);
        await MatricularAsync(aluno, cursoId);

        var remocao = await admin.DeleteAsync($"/tech-curse/Course/{cursoId}", Cancelamento);

        Assert.Equal(HttpStatusCode.Conflict, remocao.StatusCode);
        Assert.Equal("O curso possui matrículas ativas.", await DetalheDoErroAsync(remocao));
        Assert.Equal(HttpStatusCode.OK, (await aluno.GetAsync($"/tech-curse/Course/{cursoId}", Cancelamento)).StatusCode);
    }
}
