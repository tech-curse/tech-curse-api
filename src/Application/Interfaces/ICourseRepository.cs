using TechCurse.Application.DTOs;
using TechCurse.Domain.Entities;

namespace TechCurse.Application.Interfaces;

public interface ICourseRepository
{
    Task<(IEnumerable<Course> Items, int TotalCount)> GetPagedAsync(CoursePaginationParamsDto searchParams);
    Task<IEnumerable<Course>> GetAllAsync();
    Task<Course?> GetByIdAsync(int id);
    Task AddAsync(Course course);
    Task UpdateAsync(Course course);
    Task DeleteAsync(Course course);
    Task<bool> HasEnrollmentsAsync(int courseId);
}
