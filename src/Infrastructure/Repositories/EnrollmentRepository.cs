using Microsoft.EntityFrameworkCore;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Infrastructure.Data;

namespace TechCurse.Infrastructure.Repositories;

public class EnrollmentRepository : IEnrollmentRepository
{
    private readonly TechCurseContext _context;

    public EnrollmentRepository(TechCurseContext context)
    {
        _context = context;
    }

    public async Task<Enrollment?> GetByIdAsync(int id)
        => await _context.Enrollments
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.EnrollmentId == id);

    public async Task<Enrollment?> GetByStudentCourseAsync(int studentId, int courseId)
        => await _context.Enrollments
            .FirstOrDefaultAsync(e => e.StudentId == studentId && e.CourseId == courseId);

    public async Task<bool> EnrollmentIsActiveAsync(int id)
        => await _context.Enrollments
            .Where(e => e.EnrollmentId == id && e.Status).AnyAsync();

    public async Task<bool> EnrollmentIsActiveAsync(int studentId, int courseId)
        => await _context.Enrollments
            .Where(e => e.StudentId == studentId && e.CourseId == courseId && e.Status).AnyAsync();

    public async Task AddAsync(Enrollment enrollment)
    {
        await _context.Enrollments.AddAsync(enrollment);
        await _context.SaveChangesAsync();
    }
}
