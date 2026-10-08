namespace TechCurse.Domain.Entities;

public class Course
{
    public int CourseId { get; set; }
    public required string Titulo { get; set; }
    public required string Descricao { get; set; }
    public required string Categoria { get; set; }
    public required int CargaHoraria { get; set; }
    public DateTime DataCriacao { get; set; }
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}
