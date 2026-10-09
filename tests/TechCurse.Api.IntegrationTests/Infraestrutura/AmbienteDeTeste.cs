using DotNet.Testcontainers.Configurations;
using Npgsql;
using Respawn;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace TechCurse.Api.IntegrationTests.Infraestrutura;

public sealed class AmbienteDeTeste : IAsyncLifetime
{
    public const string ImagemPostgres = "public.ecr.aws/docker/library/postgres:17";

    public const string ImagemRedis = "public.ecr.aws/docker/library/redis:7";

    private readonly PostgreSqlContainer _postgres;

    private readonly RedisContainer _redis;

    private Respawner? _respawner;

    private ConnectionMultiplexer? _redisAdministrativo;

    public TechCurseApiFactory Fabrica { get; private set; } = null!;

    public AmbienteDeTeste()
    {
        TestcontainersSettings.ResourceReaperEnabled = false;
        _postgres = new PostgreSqlBuilder(ImagemPostgres).Build();
        _redis = new RedisBuilder(ImagemRedis).Build();
    }

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        Fabrica = new TechCurseApiFactory(_postgres.GetConnectionString(), _redis.GetConnectionString());
        Fabrica.CreateClient().Dispose();

        await using var conexao = new NpgsqlConnection(_postgres.GetConnectionString());
        await conexao.OpenAsync();
        _respawner = await Respawner.CreateAsync(conexao, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["__EFMigrationsHistory", "AspNetRoles", "DataProtectionKeys"]
        });

        _redisAdministrativo = await ConnectionMultiplexer.ConnectAsync($"{_redis.GetConnectionString()},allowAdmin=true");
    }

    public async ValueTask RestaurarEstadoAsync()
    {
        await using var conexao = new NpgsqlConnection(_postgres.GetConnectionString());
        await conexao.OpenAsync();
        await _respawner!.ResetAsync(conexao);

        foreach (var servidor in _redisAdministrativo!.GetServers())
        {
            await servidor.FlushAllDatabasesAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_redisAdministrativo is not null)
        {
            await _redisAdministrativo.DisposeAsync();
        }

        if (Fabrica is not null)
        {
            await Fabrica.DisposeAsync();
        }

        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }
}
