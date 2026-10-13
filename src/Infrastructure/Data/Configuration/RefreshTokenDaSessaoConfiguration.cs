using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechCurse.Infrastructure.Identity.Sessoes;

namespace TechCurse.Infrastructure.Data.Configuration;

public class RefreshTokenDaSessaoConfiguration : IEntityTypeConfiguration<RefreshTokenDaSessao>
{
    public void Configure(EntityTypeBuilder<RefreshTokenDaSessao> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.Property(t => t.Hash).IsRequired().HasMaxLength(64);
        builder.HasIndex(t => t.Hash).IsUnique();
        builder.HasOne(t => t.Sessao)
               .WithMany(s => s.RefreshTokens)
               .HasForeignKey(t => t.SessaoId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
