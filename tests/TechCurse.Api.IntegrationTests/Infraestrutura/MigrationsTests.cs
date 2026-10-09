using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TechCurse.Infrastructure.Data;

namespace TechCurse.Api.IntegrationTests.Infraestrutura;

public sealed class MigrationsTests(AmbienteDeTeste ambiente) : TesteDeIntegracao(ambiente)
{
    [Fact]
    public void Modelo_do_EF_nao_tem_mudancas_sem_migration()
    {
        using var escopo = Ambiente.Fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<TechCurseContext>();

        Assert.False(contexto.Database.HasPendingModelChanges(),
            "O modelo do EF mudou sem migration. Rode: ./scripts/com-env.sh dotnet ef migrations add <Nome> --project src/Infrastructure --startup-project src/Api");
    }
}
