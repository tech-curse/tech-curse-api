using System.Reflection.Emit;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TechCurse.Domain.Entities;

namespace TechCurse.Infrastructure.Data;

public class TechCurseContext : IdentityDbContext<IdentityUser>, IDataProtectionKeyContext
{
    public TechCurseContext(DbContextOptions<TechCurseContext> options) : base(options)
    {
    }

    public DbSet<Course> Courses { get; set; } = null!;
    public DbSet<Student> Students { get; set; } = null!;
    public DbSet<Enrollment> Enrollments { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(TechCurseContext).Assembly);

        builder.Entity<Student>().HasQueryFilter(s => !s.IsDeleted);
    }
}
