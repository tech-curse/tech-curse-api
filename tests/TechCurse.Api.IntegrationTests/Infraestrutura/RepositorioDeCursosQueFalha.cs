using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;

namespace TechCurse.Api.IntegrationTests.Infraestrutura;

public sealed class RepositorioDeCursosQueFalha : ICourseRepository
{
    public const string MensagemInterna = "Falha interna: Host=servidor-secreto;Database=producao";

    private static InvalidOperationException Falha() => new(MensagemInterna);

    public Task<(IEnumerable<Course> Items, int TotalCount)> GetPagedAsync(CoursePaginationParamsDto searchParams) => throw Falha();

    public Task<IEnumerable<Course>> GetAllAsync() => throw Falha();

    public Task<Course?> GetByIdAsync(int id) => throw Falha();

    public Task AddAsync(Course course) => throw Falha();

    public Task UpdateAsync(Course course) => throw Falha();

    public Task DeleteAsync(Course course) => throw Falha();

    public Task<bool> HasEnrollmentsAsync(int courseId) => throw Falha();
}
