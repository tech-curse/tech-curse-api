using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;

namespace TechCurse.Api.IntegrationTests.Infraestrutura;

public sealed class RepositorioDeAlunosQueFalhaAoGravar : IStudentRepository
{
    public const string Mensagem = "Falha simulada ao gravar o perfil de estudante.";

    public Task AddAsync(Student student) => throw new InvalidOperationException(Mensagem);

    public Task<(IEnumerable<Student> Items, int TotalCount)> GetPagedAsync(PaginationParamsDto searchParams) => throw new NotSupportedException();

    public Task<IEnumerable<Student>> GetAllAsync() => throw new NotSupportedException();

    public Task<Student?> GetByIdAsync(int id) => throw new NotSupportedException();

    public Task<Student?> GetByEmailAsync(string email) => throw new NotSupportedException();

    public Task<IEnumerable<CourseStudentOutputDto>> GetCoursesAsync(Student student) => throw new NotSupportedException();

    public Task UpdateAsync(Student student) => throw new NotSupportedException();

    public Task DeleteAsync(Student student) => throw new NotSupportedException();

    public Task<bool> EmailExistsAsync(string email) => Task.FromResult(false);

    public Task<bool> StudentIsActiveAsync(Student student) => throw new NotSupportedException();
}
