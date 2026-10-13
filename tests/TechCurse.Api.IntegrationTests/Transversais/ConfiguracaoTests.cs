using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TechCurse.Api.IntegrationTests.Infraestrutura;

namespace TechCurse.Api.IntegrationTests.Transversais;

public sealed partial class ConfiguracaoTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    private const string ArquivosDoWebApplicationFactory = "TechCurse.Api.settings";

    private static readonly string[] Segredos =
        ["ConnectionStrings__APITechCurse", "ConnectionStrings__RedisCache", "Jwt__SigningKey", "Seed__Admin__Email", "Seed__Admin__Password", "POSTGRES_PASSWORD"];

    [GeneratedRegex("\"(?<chave>[A-Z][A-Za-z]+(:[A-Z][A-Za-z]+)+)\"|GetConnectionString\\(\"(?<conexao>[A-Za-z]+)\"\\)")]
    private static partial Regex LeituraDeConfiguracao();

    private static string Raiz()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(diretorio.FullName, "TechCurse.slnx")))
        {
            diretorio = diretorio.Parent ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
        }

        return diretorio.FullName;
    }

    private static IEnumerable<string> ArquivosVersionaveis(string pasta, string padrao) =>
        Directory.EnumerateFiles(pasta, padrao, SearchOption.AllDirectories)
            .Where(arquivo => !arquivo.Split(Path.DirectorySeparatorChar).Any(parte => parte is "bin" or "obj"));

    private static Dictionary<string, string> EnvExample() =>
        File.ReadAllLines(Path.Combine(Raiz(), ".env.example"))
            .Where(linha => linha.Contains('=') && !linha.TrimStart().StartsWith('#'))
            .Select(linha => linha.Split('=', 2))
            .ToDictionary(partes => partes[0].Trim(), partes => partes[1].Trim());

    [Fact]
    [Trait("Especificacao", "TRV-034")]
    public void So_existe_appsettings_json_e_nenhum_User_Secrets()
    {
        var raiz = Raiz();

        var appsettings = ArquivosVersionaveis(Path.Combine(raiz, "src"), "appsettings*.json")
            .Select(arquivo => Path.GetRelativePath(raiz, arquivo).Replace('\\', '/'));
        var comUserSecrets = ArquivosVersionaveis(raiz, "*.*proj").Concat(ArquivosVersionaveis(raiz, "*.props"))
            .Where(arquivo => File.ReadAllText(arquivo).Contains("UserSecretsId", StringComparison.Ordinal));

        Assert.Equal(["src/Api/appsettings.json"], appsettings);
        Assert.Empty(comUserSecrets);
    }

    [Fact]
    [Trait("Especificacao", "TRV-034")]
    public void Variaveis_de_ambiente_prevalecem_e_nenhum_arquivo_env_e_lido()
    {
        var provedores = ((IConfigurationRoot)Ambiente.Fabrica.Services.GetRequiredService<IConfiguration>()).Providers.ToList();

        var arquivos = provedores.OfType<JsonConfigurationProvider>().Select(provedor => provedor.Source.Path!)
            .Where(arquivo => !arquivo.StartsWith(ArquivosDoWebApplicationFactory, StringComparison.Ordinal)).ToList();
        var ultimoJson = provedores.FindLastIndex(provedor => provedor is JsonConfigurationProvider);
        var variaveis = provedores.FindLastIndex(provedor => provedor is EnvironmentVariablesConfigurationProvider);

        Assert.Contains("appsettings.json", arquivos);
        Assert.All(arquivos, arquivo => Assert.Matches("^appsettings(\\.[A-Za-z]+)?\\.json$", arquivo));
        Assert.DoesNotContain(provedores, provedor => provedor.ToString()!.Contains(".env", StringComparison.OrdinalIgnoreCase));
        Assert.True(variaveis > ultimoJson, "As variáveis de ambiente precisam vir depois dos arquivos JSON.");
    }

    [Fact]
    [Trait("Especificacao", "TRV-034")]
    public void Toda_chave_lida_esta_no_env_example_e_segredo_vai_sem_valor()
    {
        var raiz = Raiz();
        var exemplo = EnvExample();

        var lidas = ArquivosVersionaveis(Path.Combine(raiz, "src"), "*.cs")
            .Where(arquivo => !arquivo.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
            .SelectMany(arquivo => LeituraDeConfiguracao().Matches(File.ReadAllText(arquivo)))
            .Select(leitura => leitura.Groups["conexao"].Success
                ? $"ConnectionStrings__{leitura.Groups["conexao"].Value}"
                : leitura.Groups["chave"].Value.Replace(":", "__"))
            .Distinct()
            .ToList();

        Assert.Contains("Jwt__SigningKey", lidas);
        Assert.All(lidas, chave => Assert.True(exemplo.ContainsKey(chave), $"{chave} falta no .env.example"));
        Assert.All(Segredos, segredo => Assert.Equal("", exemplo[segredo]));
    }

    [Fact]
    [Trait("Especificacao", "TRV-036")]
    public void Requisicoes_em_andamento_tem_ate_25_segundos_e_o_compose_espera_30()
    {
        var opcoes = Ambiente.Fabrica.Services.GetRequiredService<IOptions<HostOptions>>().Value;
        var compose = File.ReadAllText(Path.Combine(Raiz(), "compose.yaml"));

        Assert.Equal(TimeSpan.FromSeconds(25), opcoes.ShutdownTimeout);
        Assert.Contains("stop_grace_period: 30s", compose, StringComparison.Ordinal);
    }
}
