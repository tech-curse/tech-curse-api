using Microsoft.EntityFrameworkCore;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Infrastructure.Data;

namespace TechCurse.Infrastructure.Repositories;

public class CourseRepository : ICourseRepository
{
    private readonly TechCurseContext _context;

    public CourseRepository(TechCurseContext context)
    {
        _context = context;
    }

    public async Task<(IEnumerable<Course> Items, int TotalCount)> GetPagedAsync(CoursePaginationParamsDto searchParams)
    {
        var query = _context.Courses.AsQueryable().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchParams.Categoria))
        {
            query = query.Where(c => c.Categoria == searchParams.Categoria);
        }

        var totalCount = await query.CountAsync();

        query = ApplySorting(query, searchParams.SortBy, searchParams.SortDirection);

        var items = await query
            .Skip((searchParams.PageNumber - 1) * searchParams.PageSize)
            .Take(searchParams.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    private static IQueryable<Course> ApplySorting(IQueryable<Course> query, string sortBy, string sortDirection)
    {
        var isDescending = sortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);

        return (sortBy.ToLower(), isDescending) switch
        {
            ("titulo", false) => query.OrderBy(c => c.Titulo),
            ("titulo", true) => query.OrderByDescending(c => c.Titulo),

            ("categoria", false) => query.OrderBy(c => c.Categoria),
            ("categoria", true) => query.OrderByDescending(c => c.Categoria),

            ("datacriacao", false) => query.OrderBy(c => c.DataCriacao),
            ("datacriacao", true) => query.OrderByDescending(c => c.DataCriacao),

            _ => isDescending ? query.OrderByDescending(c => c.CourseId) : query.OrderBy(c => c.CourseId)
        };
    }

    public async Task<IEnumerable<Course>> GetAllAsync()
        => await _context.Courses.ToListAsync();

    public async Task<Course?> GetByIdAsync(int id)
        => await _context.Courses.FindAsync(id);

    public async Task AddAsync(Course course)
    {
        await _context.Courses.AddAsync(course);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Course course)
    {
        _context.Courses.Update(course);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Course course)
    {
        _context.Courses.Remove(course);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasEnrollmentsAsync(int courseId)
    {
        return await _context.Enrollments.AnyAsync(e => e.CourseId == courseId);
    }
}
