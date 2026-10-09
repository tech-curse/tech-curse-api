using System.Net;
using System.Net.Http.Json;
using TechCurse.Api.IntegrationTests.Infraestrutura;

namespace TechCurse.Api.IntegrationTests.Transversais;

public sealed class AcessoNegadoTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    [Fact]
    [Trait("Especificacao", "PAG-019")]
    public async Task Aluno_nao_ve_pagamentos_de_outro()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var dono = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);
        var idDoDono = await IdDoPerfilAsync(dono);
        var matriculaId = await MatricularAsync(dono, await CriarCursoAsync(admin));
        var pagamentoId = await CriarPagamentoAsync(admin, matriculaId);
        var outro = ClienteAutenticado((await RegistrarEEntrarAsync()).AccessToken);

        foreach (var rota in new[] { $"/tech-curse/Payment/{pagamentoId}", $"/tech-curse/Payment/student/{idDoDono}", $"/tech-curse/Payment/enrollment/{matriculaId}" })
        {
            var resposta = await outro.GetAsync(rota, Cancelamento);

            Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
            Assert.Equal("Você não possuí permissão suficiente para acessar este registro!", await DetalheDoErroAsync(resposta));
        }
    }

    [Fact]
    [Trait("Especificacao", "MAT-004")]
    public async Task So_aluno_e_Admin_criam_matriculas()
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        var cursoId = await CriarCursoAsync(admin);
        var instrutor = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Instructor")).AccessToken);
        var corpo = new { courseId = cursoId, studentId = 1 };

        var peloInstrutor = await instrutor.PostAsJsonAsync("/tech-curse/Enrollment", corpo, Cancelamento);
        var anonimo = await Cliente.PostAsJsonAsync("/tech-curse/Enrollment", corpo, Cancelamento);

        Assert.Equal(HttpStatusCode.Forbidden, peloInstrutor.StatusCode);
        Assert.Equal("Apenas estudantes e administradores podem criar matrículas!", await DetalheDoErroAsync(peloInstrutor));
        Assert.Equal(HttpStatusCode.Unauthorized, anonimo.StatusCode);
    }
}
