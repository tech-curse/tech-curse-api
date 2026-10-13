using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TechCurse.Api.IntegrationTests.Infraestrutura;

namespace TechCurse.Api.IntegrationTests.Cursos;

public sealed class CursosTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    private static object Curso(string titulo = "Curso", string descricao = "Descrição", string categoria = "Backend", int cargaHoraria = 10) =>
        new { titulo, descricao, categoria, cargaHoraria };

    private static async Task<string[]> TitulosAsync(HttpClient cliente, string consulta)
    {
        var pagina = await cliente.GetFromJsonAsync<JsonElement>($"/tech-curse/Course{consulta}", Cancelamento);
        return pagina.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("titulo").GetString()!).ToArray();
    }

    [Fact]
    [Trait("Especificacao", "CUR-001")]
    public async Task Catalogo_paginado_para_qualquer_usuario_autenticado()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        await CriarCursoAsync(admin, "Primeiro");
        await CriarCursoAsync(admin, "Segundo");
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);

        var pagina = await aluno.GetFromJsonAsync<JsonElement>("/tech-curse/Course", Cancelamento);

        Assert.Equal(2, pagina.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, pagina.GetProperty("pageNumber").GetInt32());
        var curso = pagina.GetProperty("items")[0];
        foreach (var campo in new[] { "id", "titulo", "descricao", "categoria", "cargaHoraria", "dataCriacao" })
        {
            Assert.True(curso.TryGetProperty(campo, out _), campo);
        }
    }

    [Fact]
    [Trait("Especificacao", "CUR-002")]
    public async Task Catalogo_exige_autenticacao()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Cliente.GetAsync("/tech-curse/Course", Cancelamento)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Cliente.GetAsync("/tech-curse/Course/1", Cancelamento)).StatusCode);
    }

    [Fact]
    [Trait("Especificacao", "CUR-003")]
    public async Task Filtro_por_categoria_e_exato()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        await CriarCursoAsync(admin, "API", "Backend");
        await CriarCursoAsync(admin, "Angular", "Frontend");

        var backend = await admin.GetFromJsonAsync<JsonElement>("/tech-curse/Course?Categoria=Backend", Cancelamento);
        var minusculo = await admin.GetFromJsonAsync<JsonElement>("/tech-curse/Course?Categoria=backend", Cancelamento);

        Assert.Equal(1, backend.GetProperty("totalCount").GetInt32());
        Assert.Equal("API", backend.GetProperty("items")[0].GetProperty("titulo").GetString());
        Assert.Equal(0, minusculo.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    [Trait("Especificacao", "CUR-004")]
    public async Task Ordenacao_por_titulo_categoria_e_data_de_criacao()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        await CriarCursoAsync(admin, "Beta", "Zeta");
        await CriarCursoAsync(admin, "Alfa", "Gama");
        await CriarCursoAsync(admin, "Gama", "Alfa");

        Assert.Equal(["Gama", "Beta", "Alfa"], await TitulosAsync(admin, "?SortBy=titulo&SortDirection=desc"));
        Assert.Equal(["Alfa", "Beta", "Gama"], await TitulosAsync(admin, "?SortBy=TITULO&SortDirection=asc"));
        Assert.Equal(["Gama", "Alfa", "Beta"], await TitulosAsync(admin, "?SortBy=categoria"));
        Assert.Equal(["Beta", "Alfa", "Gama"], await TitulosAsync(admin, "?SortBy=categoria&SortDirection=desc"));
        Assert.Equal(["Beta", "Alfa", "Gama"], await TitulosAsync(admin, "?SortBy=datacriacao"));
        Assert.Equal(["Gama", "Alfa", "Beta"], await TitulosAsync(admin, "?SortBy=datacriacao&SortDirection=desc"));
    }

    [Fact]
    [Trait("Especificacao", "CUR-005")]
    [Trait("Especificacao", "CUR-006")]
    public async Task Detalhe_de_curso_existente_e_404_para_inexistente()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var id = await CriarCursoAsync(admin, "Detalhado");

        var existente = await admin.GetFromJsonAsync<JsonElement>($"/tech-curse/Course/{id}", Cancelamento);
        var inexistente = await admin.GetAsync("/tech-curse/Course/999999", Cancelamento);

        Assert.Equal("Detalhado", existente.GetProperty("titulo").GetString());
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.Equal("Curso não encontrado.", await DetalheDoErroAsync(inexistente));
    }

    [Theory]
    [Trait("Especificacao", "CUR-007")]
    [InlineData("Admin")]
    [InlineData("Instructor")]
    public async Task Admin_ou_Instructor_cria_curso(string papel)
    {
        var cliente = ClienteAutenticado((await CriarUsuarioEEntrarAsync(papel)).AccessToken);
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);

        var resposta = await cliente.PostAsJsonAsync("/tech-curse/Course", Curso("Novo"), Cancelamento);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        var id = corpo.GetProperty("id").GetInt32();
        Assert.True(corpo.TryGetProperty("dataCriacao", out _));
        Assert.EndsWith($"/tech-curse/Course/{id}", resposta.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Novo", await TitulosAsync(aluno, ""));
    }

    [Fact]
    [Trait("Especificacao", "CUR-008")]
    public async Task Aluno_nao_cria_curso()
    {
        var aluno = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await aluno.PostAsJsonAsync("/tech-curse/Course", Curso(), Cancelamento)).StatusCode);
    }

    public static TheoryData<object, string, string> CursosInvalidos => new()
    {
        { Curso(titulo: ""), "Titulo", "O título é obrigatório." },
        { Curso(titulo: new string('t', 101)), "Titulo", "O título deve ter no máximo 100 caracteres." },
        { Curso(descricao: ""), "Descricao", "A descrição é obrigatória." },
        { Curso(categoria: ""), "Categoria", "A categoria é obrigatória." },
        { Curso(categoria: new string('c', 51)), "Categoria", "A categoria deve ter no máximo 50 caracteres." },
        { Curso(cargaHoraria: 0), "CargaHoraria", "A carga horária deve ser maior que zero." }
    };

    [Theory]
    [Trait("Especificacao", "CUR-009")]
    [MemberData(nameof(CursosInvalidos))]
    public async Task Dados_invalidos_na_criacao_sao_recusados(object curso, string campo, string mensagem)
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);

        var resposta = await admin.PostAsJsonAsync("/tech-curse/Course", curso, Cancelamento);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        var erros = (await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)).GetProperty("errors");
        Assert.Contains(mensagem, erros.GetProperty(campo).EnumerateArray().Select(e => e.GetString()));
    }

    [Theory]
    [Trait("Especificacao", "CUR-012")]
    [MemberData(nameof(CursosInvalidos))]
    public async Task Edicao_valida_os_mesmos_campos_da_criacao(object curso, string campo, string mensagem)
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var id = await CriarCursoAsync(admin);

        var resposta = await admin.PutAsJsonAsync($"/tech-curse/Course/{id}", curso, Cancelamento);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        var erros = (await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)).GetProperty("errors");
        Assert.Contains(mensagem, erros.GetProperty(campo).EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    [Trait("Especificacao", "CUR-010")]
    public async Task Admin_edita_curso_sem_mudar_a_data_de_criacao()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var id = await CriarCursoAsync(admin, "Antes");
        var antes = await admin.GetFromJsonAsync<JsonElement>($"/tech-curse/Course/{id}", Cancelamento);

        var resposta = await admin.PutAsJsonAsync($"/tech-curse/Course/{id}", Curso("Depois", "Nova descrição", "Dados", 20), Cancelamento);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        var depois = await admin.GetFromJsonAsync<JsonElement>($"/tech-curse/Course/{id}", Cancelamento);
        Assert.Equal("Depois", depois.GetProperty("titulo").GetString());
        Assert.Equal("Dados", depois.GetProperty("categoria").GetString());
        Assert.Equal(20, depois.GetProperty("cargaHoraria").GetInt32());
        Assert.Equal(antes.GetProperty("dataCriacao").GetDateTime(), depois.GetProperty("dataCriacao").GetDateTime(), TimeSpan.FromMilliseconds(1));
    }

    [Theory]
    [Trait("Especificacao", "CUR-011")]
    [Trait("Especificacao", "CUR-016")]
    [InlineData("Instructor")]
    [InlineData("Student")]
    public async Task So_Admin_edita_e_remove(string papel)
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var id = await CriarCursoAsync(admin);
        var cliente = ClienteAutenticado((await CriarUsuarioEEntrarAsync(papel)).AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PutAsJsonAsync($"/tech-curse/Course/{id}", Curso(), Cancelamento)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.DeleteAsync($"/tech-curse/Course/{id}", Cancelamento)).StatusCode);
    }

    [Fact]
    [Trait("Especificacao", "CUR-013")]
    [Trait("Especificacao", "CUR-017")]
    public async Task Editar_ou_remover_curso_inexistente_responde_404()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);

        var edicao = await admin.PutAsJsonAsync("/tech-curse/Course/999999", Curso(), Cancelamento);
        var remocao = await admin.DeleteAsync("/tech-curse/Course/999999", Cancelamento);

        Assert.Equal(HttpStatusCode.NotFound, edicao.StatusCode);
        Assert.Equal("Curso não encontrado.", await DetalheDoErroAsync(edicao));
        Assert.Equal(HttpStatusCode.NotFound, remocao.StatusCode);
        Assert.Equal("Curso não encontrado.", await DetalheDoErroAsync(remocao));
    }

    [Fact]
    [Trait("Especificacao", "CUR-014")]
    public async Task Admin_remove_curso_sem_matriculas()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var id = await CriarCursoAsync(admin);

        var remocao = await admin.DeleteAsync($"/tech-curse/Course/{id}", Cancelamento);

        Assert.Equal(HttpStatusCode.NoContent, remocao.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/tech-curse/Course/{id}", Cancelamento)).StatusCode);
        Assert.Empty(await TitulosAsync(admin, ""));
    }
}
