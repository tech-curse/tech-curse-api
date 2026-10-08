using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using TechCurse.Infrastructure.Data;

namespace TechCurse.Api.Configuration;

public static class DataProtectionSetup
{
    private const string NomeDaAplicacao = "TechCurse";

    public static IServiceCollection AddDataProtectionSetup(this IServiceCollection services)
    {
        services
            .AddDataProtection()
            .SetApplicationName(NomeDaAplicacao)
            .PersistKeysToDbContext<TechCurseContext>();

        return services;
    }
}
