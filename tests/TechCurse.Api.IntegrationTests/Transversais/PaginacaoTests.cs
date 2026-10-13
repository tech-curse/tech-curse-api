using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TechCurse.Api.IntegrationTests.Infraestrutura;

namespace TechCurse.Api.IntegrationTests.Transversais;

public sealed class PaginacaoTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    private async Task<HttpClient> AdminComCursosAsync(int quantidade)
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);
        for (var i = 1; i <= quantidade; i++)
        {
            await CriarCursoAsync(admin, $"Curso {i:D2}");
        }

        return admin;
    }

    [Fact]
    [Trait("Especificacao", "TRV-027")]
    public async Task Pagina_e_tamanho_padrao()
    {
        var admin = await AdminComCursosAsync(12);

        var pagina = await admin.GetFromJsonAsync<JsonElement>("/tech-curse/Course", Cancelamento);

        Assert.Equal(1, pagina.GetProperty("pageNumber").GetInt32());
        Assert.Equal(10, pagina.GetProperty("pageSize").GetInt32());
        var ids = pagina.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt32()).ToArray();
        Assert.Equal(10, ids.Length);
        Assert.Equal(ids.Order(), ids);
    }

    [Fact]
    [Trait("Especificacao", "TRV-028")]
    public async Task Tamanho_de_pagina_tem_teto_de_50()
    {
        var admin = await AdminComCursosAsync(1);

        var pagina = await admin.GetFromJsonAsync<JsonElement>("/tech-curse/Course?PageSize=500", Cancelamento);

        Assert.Equal(50, pagina.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    [Trait("Especificacao", "TRV-029")]
    public async Task Metadados_descrevem_a_pagina()
    {
        var admin = await AdminComCursosAsync(25);

        var pagina = await admin.GetFromJsonAsync<JsonElement>("/tech-curse/Course?PageNumber=3&PageSize=10", Cancelamento);

        Assert.Equal(5, pagina.GetProperty("items").GetArrayLength());
        Assert.Equal(25, pagina.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, pagina.GetProperty("totalPages").GetInt32());
        Assert.True(pagina.GetProperty("hasPreviousPage").GetBoolean());
        Assert.False(pagina.GetProperty("hasNextPage").GetBoolean());
    }

    [Fact]
    [Trait("Especificacao", "TRV-030")]
    public async Task Pagina_alem_da_ultima_vem_vazia()
    {
        var admin = await AdminComCursosAsync(3);

        var resposta = await admin.GetAsync("/tech-curse/Course?PageNumber=9", Cancelamento);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var pagina = await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento);
        Assert.Equal(0, pagina.GetProperty("items").GetArrayLength());
        Assert.Equal(3, pagina.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [Trait("Especificacao", "TRV-031")]
    [InlineData("/tech-curse/Course?PageNumber=0", "PageNumber", "A página deve ser maior ou igual a 1.")]
    [InlineData("/tech-curse/Course?PageSize=0", "PageSize", "O tamanho da página deve ser maior ou igual a 1.")]
    [InlineData("/tech-curse/Student?PageNumber=-1", "PageNumber", "A página deve ser maior ou igual a 1.")]
    [InlineData("/tech-curse/Payment?PageSize=-5", "PageSize", "O tamanho da página deve ser maior ou igual a 1.")]
    public async Task Pagina_ou_tamanho_invalido_e_recusado(string rota, string campo, string mensagem)
    {
        var admin = ClienteAutenticado((await CriarUsuarioEEntrarAsync("Admin")).AccessToken);

        var resposta = await admin.GetAsync(rota, Cancelamento);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        var erros = (await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento)).GetProperty("errors");
        Assert.Equal(mensagem, erros.GetProperty(campo)[0].GetString());
    }

    [Fact]
    [Trait("Especificacao", "TRV-032")]
    public async Task Ordenacao_desconhecida_cai_no_padrao()
    {
        var admin = await AdminComCursosAsync(3);

        var resposta = await admin.GetAsync("/tech-curse/Course?SortBy=campo-inexistente&SortDirection=desc", Cancelamento);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var ids = (await resposta.Content.ReadFromJsonAsync<JsonElement>(Cancelamento))
            .GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt32()).ToArray();
        Assert.Equal(ids.OrderDescending(), ids);
    }
}
