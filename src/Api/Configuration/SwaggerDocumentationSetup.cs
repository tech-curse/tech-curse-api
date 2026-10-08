using System.Reflection;
using Microsoft.OpenApi;

namespace TechCurse.Api.Configuration;

public static class SwaggerDocumentationSetup
{
    public static IServiceCollection AddSwaggerDocumentationSetup(this IServiceCollection services, IConfiguration configuration)
    {
        string securitySchemeName = "Bearer";

        var versao = typeof(SwaggerDocumentationSetup).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            .Split('+')[0] ?? "desconhecida";

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Tech Curse API",
                Version = versao,
                Description = "API para gestão de cursos e alunos baseada em Clean Architecture."
            });

            c.AddSecurityDefinition(securitySchemeName, new OpenApiSecurityScheme
            {
                Description = "Insira o token JWT desta maneira: Bearer {seu_token}",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = securitySchemeName
            });

            c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference(securitySchemeName, document), new List<string>() }
            });

            c.EnableAnnotations();
        });
        return services;
    }
}
