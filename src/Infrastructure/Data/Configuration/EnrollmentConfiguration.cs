using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechCurse.Domain.Entities;

namespace TechCurse.Infrastructure.Data.Configuration;

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.HasIndex(e => new { e.StudentId, e.CourseId })
               .IsUnique();
    }
}
