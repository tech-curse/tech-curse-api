using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechCurse.Domain.Entities;

namespace TechCurse.Infrastructure.Data.Configuration;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(p => p.PaymentId);

        builder.Property(p => p.Amount)
               .HasPrecision(18, 2)
               .IsRequired();

        builder.Property(p => p.Status)
               .HasConversion<int>()
               .IsRequired();

        builder.Property(p => p.IsActive)
               .IsRequired();

        builder.Property(p => p.ExternalTransactionId)
               .HasMaxLength(200);

        builder.Property(p => p.CreatedAt)
               .IsRequired();

        builder.HasIndex(p => p.StudentId);
        builder.HasIndex(p => p.Status);

        builder.HasIndex(p => p.EnrollmentId)
               .IsUnique()
               .HasFilter("\"IsActive\" = true");

        builder.HasOne(p => p.Enrollment)
               .WithMany()
               .HasForeignKey(p => p.EnrollmentId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Student)
               .WithMany()
               .HasForeignKey(p => p.StudentId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
