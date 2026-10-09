using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TechCurse.Api.IntegrationTests.Infraestrutura;

namespace TechCurse.Api.IntegrationTests.Alunos;

public sealed class RemocaoDeAlunoTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    [Fact]
    [Trait("Especificacao", "ALU-014")]
    public async Task Admin_remove_aluno_que_some_das_consultas_e_nao_consegue_mais_entrar()
    {
        var aluno = await RegistrarEEntrarAsync();
        var perfil = await ClienteAutenticado(aluno.AccessToken)
            .GetFromJsonAsync<JsonElement>("/tech-curse/Student/me", Cancelamento);
        var id = perfil.GetProperty("id").GetInt32();
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);

        var remocao = await admin.DeleteAsync($"/tech-curse/Student/{id}", Cancelamento);

        Assert.Equal(HttpStatusCode.NoContent, remocao.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/tech-curse/Student/{id}", Cancelamento)).StatusCode);
        var lista = await admin.GetFromJsonAsync<JsonElement>("/tech-curse/Student", Cancelamento);
        Assert.DoesNotContain(lista.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetInt32() == id);
        var login = await Cliente.PostAsJsonAsync("/tech-curse/Auth/login",
            new { email = aluno.Email, password = SenhaValida }, Cancelamento);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }
}
