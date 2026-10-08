using TechCurse.Application.DTOs;
using TechCurse.Domain.Entities;

namespace TechCurse.Application.Interfaces;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(int id);
    Task<(IEnumerable<Payment> Items, int TotalCount)> GetByStudentIdAsync(int studentId, PaginationParamsDto searchParams);
    Task<IEnumerable<Payment>> GetByEnrollmentIdAsync(int enrollmentId);
    Task<bool> ExistsActiveByEnrollmentAsync(int enrollmentId);
    Task<(IEnumerable<Payment> Items, int TotalCount)> GetPagedAsync(PaginationParamsDto searchParams);
    Task AddAsync(Payment payment);
    Task UpdateAsync(Payment payment);
    Task DeleteAsync(Payment payment);
}
