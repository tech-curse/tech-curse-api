using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IPNetwork = System.Net.IPNetwork;

namespace TechCurse.Api.Configuration;

public static class ForwardedHeadersSetup
{
    private const string ChaveProxiesConhecidos = "ForwardedHeaders:KnownProxies";
    private const string ChaveRedesConhecidas = "ForwardedHeaders:KnownNetworks";

    public static IServiceCollection AddForwardedHeadersSetup(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var proxies = LerLista(configuration, ChaveProxiesConhecidos)
            .Select(ConverterProxy)
            .ToArray();

        var redes = LerLista(configuration, ChaveRedesConhecidas)
            .Select(ConverterRede)
            .ToArray();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(proxy);
            }

            foreach (var rede in redes)
            {
                options.KnownIPNetworks.Add(rede);
            }
        });

        return services;
    }

    private static string[] LerLista(IConfiguration configuration, string chave)
    {
        var configurado = configuration.GetValue<string>(chave);

        if (string.IsNullOrWhiteSpace(configurado))
        {
            return [];
        }

        return configurado.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IPAddress ConverterProxy(string valor)
    {
        if (!IPAddress.TryParse(valor, out var endereco))
        {
            throw new InvalidOperationException(
                $"O valor '{valor}' em '{ChaveProxiesConhecidos}' não é um endereço IP válido.");
        }

        return endereco;
    }

    private static IPNetwork ConverterRede(string valor)
    {
        if (!IPNetwork.TryParse(valor, out var rede))
        {
            throw new InvalidOperationException(
                $"O valor '{valor}' em '{ChaveRedesConhecidas}' não é uma rede em notação CIDR (ex.: 172.18.0.0/16).");
        }

        return rede;
    }
}
