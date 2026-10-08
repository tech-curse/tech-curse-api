using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TechCurse.Infrastructure.Data;

public static class DbInitializer
{
    private const string ChaveEmailAdmin = "Seed:Admin:Email";
    private const string ChaveSenhaAdmin = "Seed:Admin:Password";
    private const string CategoriaDoLog = "TechCurse.Infrastructure.Data.DbInitializer";

    public static async Task SeedDataAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        string[] roles = { "Admin", "Instructor", "Student" };

        foreach (var roleName in roles)
        {
            var roleExists = await roleManager.RoleExistsAsync(roleName);
            if (!roleExists)
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        await SemearAdminDeDesenvolvimentoAsync(serviceProvider);
    }

    private static async Task SemearAdminDeDesenvolvimentoAsync(IServiceProvider serviceProvider)
    {
        var ambiente = serviceProvider.GetRequiredService<IHostEnvironment>();

        if (!ambiente.IsDevelopment())
        {
            return;
        }

        var configuracao = serviceProvider.GetRequiredService<IConfiguration>();
        var email = configuracao[ChaveEmailAdmin]?.Trim();
        var senha = configuracao[ChaveSenhaAdmin];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
        {
            return;
        }

        var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var usuarioExistente = await userManager.FindByEmailAsync(email);

        if (usuarioExistente is not null)
        {
            if (!await userManager.IsInRoleAsync(usuarioExistente, "Admin"))
            {
                var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(CategoriaDoLog);
                logger.LogWarning(
                    "O e-mail {Email} configurado para o Admin semeado já pertence a um usuário sem a role Admin; o seed não alterou esse usuário.",
                    email);
            }

            return;
        }

        var admin = new IdentityUser
        {
            UserName = email.Split('@')[0],
            Email = email,
            EmailConfirmed = true
        };

        var resultadoCriacao = await userManager.CreateAsync(admin, senha);

        if (!resultadoCriacao.Succeeded)
        {
            throw new InvalidOperationException(FormatarErroDeSeed(resultadoCriacao));
        }

        var resultadoRole = await userManager.AddToRoleAsync(admin, "Admin");

        if (!resultadoRole.Succeeded)
        {
            throw new InvalidOperationException(FormatarErroDeSeed(resultadoRole));
        }
    }

    private static string FormatarErroDeSeed(IdentityResult resultado)
    {
        var erros = string.Join("; ", resultado.Errors.Select(e => $"{e.Code}: {e.Description}"));
        return $"Não foi possível criar o Admin semeado: {erros}";
    }
}
