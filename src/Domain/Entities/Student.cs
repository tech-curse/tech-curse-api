using Microsoft.AspNetCore.Identity;

namespace TechCurse.Domain.Entities;

public class Student
{
    public int StudentId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string IdentityUserId { get; set; } = string.Empty;
    public IdentityUser IdentityUser { get; set; } = null!;
    public DateTime DataCadastro { get; set; }
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}
