using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TechCurse.Api.IntegrationTests.Infraestrutura;

namespace TechCurse.Api.IntegrationTests.Transversais;

public sealed class ProxyReversoTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    private const string CabecalhoDoIpRemoto = "X-Teste-Ip-Remoto";

    private sealed class IpRemotoDeTeste : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (contexto, proximo) =>
            {
                if (contexto.Request.Headers.TryGetValue(CabecalhoDoIpRemoto, out var ip))
                {
                    contexto.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());
                }

                await proximo();
            });
            next(app);
        };
    }

    private WebApplicationFactory<Program> FabricaAtrasDoProxy(params (string Chave, string Valor)[] configuracoes) =>
        Ambiente.Fabrica.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:Enabled", "true");
            builder.UseSetting("RateLimiting:AuthPermitLimit", "1");
            builder.UseSetting("RateLimiting:AuthWindowSeconds", "60");
            foreach (var (chave, valor) in configuracoes)
            {
                builder.UseSetting(chave, valor);
            }

            builder.ConfigureTestServices(servicos => servicos.AddTransient<IStartupFilter, IpRemotoDeTeste>());
        });

    private static Task<HttpResponseMessage> EntrarPorAsync(HttpClient cliente, string ipRemoto, string ipDoCliente)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/tech-curse/Auth/login")
        {
            Content = JsonContent.Create(new { email = "ninguem@teste.dev", password = "Qualquer@Senha1" })
        };
        requisicao.Headers.Add(CabecalhoDoIpRemoto, ipRemoto);
        requisicao.Headers.Add("X-Forwarded-For", ipDoCliente);
        return cliente.SendAsync(requisicao, Cancelamento);
    }

    [Theory]
    [Trait("Especificacao", "TRV-013")]
    [InlineData("ForwardedHeaders:KnownProxies", "10.0.0.1")]
    [InlineData("ForwardedHeaders:KnownNetworks", "10.0.0.0/24")]
    public async Task Atras_do_proxy_confiavel_cada_cliente_tem_o_proprio_limite(string chave, string valor)
    {
        await using var fabrica = FabricaAtrasDoProxy((chave, valor));
        using var cliente = fabrica.CreateClient();

        var primeiroCliente = await EntrarPorAsync(cliente, "10.0.0.1", "203.0.113.1");
        var segundoCliente = await EntrarPorAsync(cliente, "10.0.0.1", "203.0.113.2");
        var primeiroDeNovo = await EntrarPorAsync(cliente, "10.0.0.1", "203.0.113.1");

        Assert.Equal(HttpStatusCode.Unauthorized, primeiroCliente.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, segundoCliente.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, primeiroDeNovo.StatusCode);
    }

    [Fact]
    [Trait("Especificacao", "TRV-013")]
    public async Task X_Forwarded_For_de_quem_nao_e_proxy_confiavel_e_ignorado()
    {
        await using var fabrica = FabricaAtrasDoProxy(("ForwardedHeaders:KnownProxies", "10.0.0.1"));
        using var cliente = fabrica.CreateClient();

        var primeira = await EntrarPorAsync(cliente, "198.51.100.7", "203.0.113.1");
        var segunda = await EntrarPorAsync(cliente, "198.51.100.7", "203.0.113.2");

        Assert.Equal(HttpStatusCode.Unauthorized, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, segunda.StatusCode);
    }

    [Theory]
    [Trait("Especificacao", "TRV-013")]
    [InlineData("ForwardedHeaders:KnownProxies", "nao-e-ip")]
    [InlineData("ForwardedHeaders:KnownNetworks", "10.0.0.0/99")]
    public void Proxy_configurado_com_valor_invalido_impede_a_subida(string chave, string valor)
    {
        using var fabrica = Ambiente.Fabrica.WithWebHostBuilder(builder => builder.UseSetting(chave, valor));

        var erro = Assert.Throws<InvalidOperationException>(() => fabrica.CreateClient());

        Assert.Contains(chave, erro.Message, StringComparison.Ordinal);
        Assert.Contains(valor, erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Especificacao", "TRV-037")]
    public async Task A_API_atende_HTTP_sem_redirecionar_para_HTTPS()
    {
        await using var fabrica = Ambiente.Fabrica.WithWebHostBuilder(builder => builder.UseSetting("https_port", "443"));
        using var cliente = fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var resposta = await cliente.GetAsync("/health/live", Cancelamento);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Null(resposta.Headers.Location);
    }
}
