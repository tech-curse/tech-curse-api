using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TechCurse.Api.IntegrationTests.Infraestrutura;

public sealed class TechCurseApiFactory(string conexaoPostgres, string conexaoRedis) : WebApplicationFactory<Program>
{
    public const string Emissor = "tech-curse-testes";

    public const string Audiencia = "tech-curse-testes";

    public string ChaveDeAssinatura { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:APITechCurse", conexaoPostgres);
        builder.UseSetting("ConnectionStrings:RedisCache", conexaoRedis);
        builder.UseSetting("Jwt:Issuer", Emissor);
        builder.UseSetting("Jwt:Audience", Audiencia);
        builder.UseSetting("Jwt:SigningKey", ChaveDeAssinatura);
        builder.UseSetting("RateLimiting:Enabled", "false");
        builder.UseSetting("Cors:AllowedOrigins", "http://localhost:4200");
    }
}
