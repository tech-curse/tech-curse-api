using Microsoft.EntityFrameworkCore;
using TechCurse.Infrastructure.Data;

namespace TechCurse.Api.Configuration;

public static class EFCoreSetup
{
    public static IServiceCollection AddEFCoreSetup(this IServiceCollection services, IConfiguration configuration)
    {
        var apiConnectionString = configuration.GetConnectionString("APITechCurse");

        if (string.IsNullOrWhiteSpace(apiConnectionString))
        {
            throw new InvalidOperationException("A connection string 'APITechCurse' não está configurada.");
        }

        services.AddDbContext<TechCurseContext>(options =>
        {
            options.UseNpgsql(
                apiConnectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
                });
        });

        services.AddHealthChecks().AddNpgSql(
            apiConnectionString,
            name: "Database_Postgres",
            tags: [HealthCheckTags.Ready]);

        return services;
    }
}
