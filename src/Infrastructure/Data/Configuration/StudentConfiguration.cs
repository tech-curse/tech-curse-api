using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechCurse.Domain.Entities;

namespace TechCurse.Infrastructure.Data.Configuration;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.HasIndex(s => s.Email)
               .IsUnique();

        builder.Property(s => s.Email)
               .IsRequired()
               .HasMaxLength(150);
    }
}
