using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using TechCurse.Api.Administracao;
using TechCurse.Api.Configuration;
using TechCurse.Api.Middleware;
using TechCurse.Application;
using TechCurse.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<HostOptions>(opcoes => opcoes.ShutdownTimeout = EncerramentoSetup.TempoMaximo);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    .AddPagamentosSetup(builder.Configuration);

builder.Services.AddSerilogSetup(builder.Configuration);
builder.Services.AddEFCoreSetup(builder.Configuration);
builder.Services.AddIdentityAuthenticationSetup(builder.Configuration);
builder.Services.AddRedisCacheSetup(builder.Configuration);
builder.Services.AddDataProtectionSetup();
builder.Services.AddRateLimitingSetup(builder.Configuration);
builder.Services.AddCorsSetup(builder.Configuration);
builder.Services.AddForwardedHeadersSetup(builder.Configuration);

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerDocumentationSetup(builder.Configuration);

var app = builder.Build();

if (ComandosAdministrativos.EhComando(args))
{
    Environment.ExitCode = await ComandosAdministrativos.ExecutarAsync(args, app.Services, CancellationToken.None);
    await app.DisposeAsync();
    return;
}

app.UseForwardedHeaders();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseCors(CorsSetup.PoliticaFrontend);

if (app.Configuration.GetValue("Swagger:Enabled", false))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tech Curse API v1"));
}

app.UseAuthentication();

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains(HealthCheckTags.Ready),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        if (!context.User.IsInRole("Admin"))
        {
            await context.Response.WriteAsJsonAsync(new
            {
                status = report.Status.ToString()
            });

            return;
        }

        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            duracaoMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                nome = entry.Key,
                status = entry.Value.Status.ToString(),
                duracaoMs = entry.Value.Duration.TotalMilliseconds,
                descricao = entry.Value.Description,
                erro = entry.Value.Exception?.Message
            })
        });
    }
});

app.Run();
