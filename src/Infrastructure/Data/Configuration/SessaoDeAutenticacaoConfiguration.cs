using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechCurse.Infrastructure.Identity.Sessoes;

namespace TechCurse.Infrastructure.Data.Configuration;

public class SessaoDeAutenticacaoConfiguration : IEntityTypeConfiguration<SessaoDeAutenticacao>
{
    public void Configure(EntityTypeBuilder<SessaoDeAutenticacao> builder)
    {
        builder.ToTable("SessoesDeAutenticacao");
        builder.Property(s => s.UsuarioId).IsRequired().HasMaxLength(450);
        builder.Property(s => s.MotivoDaRevogacao).HasMaxLength(30);
        builder.HasIndex(s => s.UsuarioId);
        builder.HasOne<IdentityUser>()
               .WithMany()
               .HasForeignKey(s => s.UsuarioId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
