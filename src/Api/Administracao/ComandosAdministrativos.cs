using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TechCurse.Infrastructure.Data;

namespace TechCurse.Api.Administracao;

public static class ComandosAdministrativos
{
    public const string Migrar = "migrar";

    public const string CriarAdmin = "criar-admin";

    public const string ChaveEmailDoAdmin = "CriarAdmin:Email";

    public const string ChaveSenhaDoAdmin = "CriarAdmin:Password";

    private const string CategoriaDoLog = "TechCurse.Api.Administracao";

    public static bool EhComando(string[] args) => args.Length > 0 && args[0] is Migrar or CriarAdmin;

    public static async Task<int> ExecutarAsync(string[] args, IServiceProvider servicos, CancellationToken cancelamento)
    {
        var logger = servicos.GetRequiredService<ILoggerFactory>().CreateLogger(CategoriaDoLog);

        if (args.Length != 1)
        {
            logger.LogError(
                "O comando {Comando} não aceita argumentos. Credenciais e opções vêm só de variáveis de ambiente.", args[0]);
            return 2;
        }

        await using var escopo = servicos.CreateAsyncScope();

        try
        {
            return args[0] switch
            {
                Migrar => await MigrarAsync(escopo.ServiceProvider, logger, cancelamento),
                _ => await CriarAdminAsync(escopo.ServiceProvider, logger)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "O comando {Comando} falhou.", args[0]);
            return 1;
        }
    }

    private static async Task<int> MigrarAsync(IServiceProvider servicos, ILogger logger, CancellationToken cancelamento)
    {
        var contexto = servicos.GetRequiredService<TechCurseContext>();

        var pendentes = (await contexto.Database.GetPendingMigrationsAsync(cancelamento)).ToList();
        await contexto.Database.MigrateAsync(cancelamento);
        await DbInitializer.SeedDataAsync(servicos);

        logger.LogInformation(
            "Banco atualizado: {Quantidade} migration(s) aplicada(s) ({Migrations}); roles conferidas.",
            pendentes.Count,
            pendentes.Count == 0 ? "nenhuma pendente" : string.Join(", ", pendentes));
        return 0;
    }

    private static async Task<int> CriarAdminAsync(IServiceProvider servicos, ILogger logger)
    {
        var configuracao = servicos.GetRequiredService<IConfiguration>();
        var email = configuracao[ChaveEmailDoAdmin]?.Trim();
        var senha = configuracao[ChaveSenhaDoAdmin];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(senha))
        {
            logger.LogError("Defina CriarAdmin__Email e CriarAdmin__Password nas variáveis de ambiente do comando.");
            return 2;
        }

        var roles = servicos.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roles.RoleExistsAsync("Admin"))
        {
            logger.LogError("A role Admin não existe. Rode o comando migrar antes.");
            return 1;
        }

        var usuarios = servicos.GetRequiredService<UserManager<IdentityUser>>();

        if (await usuarios.FindByEmailAsync(email) is not null)
        {
            logger.LogInformation("Já existe um usuário com o e-mail informado; nada foi alterado.");
            return 0;
        }

        var admin = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var criacao = await usuarios.CreateAsync(admin, senha);

        if (!criacao.Succeeded)
        {
            logger.LogError("O Admin não foi criado: {Erros}", Descrever(criacao));
            return 1;
        }

        var atribuicao = await usuarios.AddToRoleAsync(admin, "Admin");

        if (!atribuicao.Succeeded)
        {
            await usuarios.DeleteAsync(admin);
            logger.LogError("O Admin não foi criado: {Erros}", Descrever(atribuicao));
            return 1;
        }

        logger.LogInformation("Admin criado.");
        return 0;
    }

    private static string Descrever(IdentityResult resultado) =>
        string.Join(" ", resultado.Errors.Select(erro => erro.Description));
}
