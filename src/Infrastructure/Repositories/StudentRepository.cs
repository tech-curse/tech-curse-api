using Microsoft.EntityFrameworkCore;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Infrastructure.Data;

namespace TechCurse.Infrastructure.Repositories;

public class StudentRepository : IStudentRepository
{
    private readonly TechCurseContext _context;

    public StudentRepository(TechCurseContext context)
    {
        _context = context;
    }

    public async Task<(IEnumerable<Student> Items, int TotalCount)> GetPagedAsync(PaginationParamsDto searchParams)
    {
        var query = _context.Students.AsQueryable().AsNoTracking();

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((searchParams.PageNumber - 1) * searchParams.PageSize)
            .Take(searchParams.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<IEnumerable<Student>> GetAllAsync()
        => await _context.Students.ToListAsync();

    public async Task<Student?> GetByIdAsync(int id)
        => await _context.Students.FindAsync(id);

    public async Task<Student?> GetByEmailAsync(string email)
        => await _context.Students.FirstOrDefaultAsync(s => s.Email == email);

    public async Task<IEnumerable<CourseStudentOutputDto>> GetCoursesAsync(Student student)
    {
        var courses = await _context.Enrollments
            .Where(e => e.StudentId == student.StudentId)
            .Select(e => new CourseStudentOutputDto(
                e.CourseId,
                e.Course.Titulo,
                e.Course.Descricao,
                e.Course.Categoria,
                e.Status,
                e.EnrollmentId
            ))
            .ToListAsync();

        return courses;
    }

    public async Task AddAsync(Student student)
    {
        try
        {
            await _context.Students.AddAsync(student);
            await _context.SaveChangesAsync();
        }
        catch
        {
            _context.Entry(student).State = EntityState.Detached;
            throw;
        }
    }

    public async Task UpdateAsync(Student student)
    {
        _context.Students.Update(student);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Student student)
    {
        _context.Students.Remove(student);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Students.AnyAsync(s => s.Email == email);
    }

    public async Task<bool> StudentIsActiveAsync(Student student)
    {
        return await _context.Students.Where(s => s.StudentId == student.StudentId && s.IsDeleted == false).AnyAsync();
    }
}
