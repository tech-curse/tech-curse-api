using Serilog;
using Serilog.Formatting.Json;

namespace TechCurse.Api.Configuration;

public static class SerilogSetup
{
    public static IServiceCollection AddSerilogSetup(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSerilog((serviceProvider, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console(new JsonFormatter());
        });

        return services;
    }
}
