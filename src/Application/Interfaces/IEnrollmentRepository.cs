using TechCurse.Application.DTOs;
using TechCurse.Domain.Entities;

namespace TechCurse.Application.Interfaces;

public interface IEnrollmentRepository
{
    Task<Enrollment?> GetByIdAsync(int id);
    Task<Enrollment?> GetByStudentCourseAsync(int studentId, int courseId);
    Task<bool> EnrollmentIsActiveAsync(int id);
    Task<bool> EnrollmentIsActiveAsync(int studentId, int courseId);
    Task AddAsync(Enrollment enrollment);
}
