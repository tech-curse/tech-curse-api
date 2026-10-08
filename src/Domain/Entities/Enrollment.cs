namespace TechCurse.Domain.Entities;

public class Enrollment
{
    public int EnrollmentId { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public bool Status { get; set; } = true;
    public DateTime DataMatricula { get; set; }
}
