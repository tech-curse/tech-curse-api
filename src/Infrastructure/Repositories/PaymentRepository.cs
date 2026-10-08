using Microsoft.EntityFrameworkCore;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Infrastructure.Data;

namespace TechCurse.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly TechCurseContext _context;

    public PaymentRepository(TechCurseContext context)
    {
        _context = context;
    }

    public async Task<Payment?> GetByIdAsync(int id)
    {
        return await _context.Payments
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(p => p.Student)
            .Include(p => p.Enrollment)
                .ThenInclude(e => e.Course)
            .FirstOrDefaultAsync(p => p.PaymentId == id);
    }

    public async Task<(IEnumerable<Payment> Items, int TotalCount)> GetByStudentIdAsync(int studentId, PaginationParamsDto searchParams)
    {
        var query = _context.Payments
            .AsQueryable()
            .AsNoTracking()
            .Include(p => p.Enrollment)
                .ThenInclude(e => e.Course)
            .Where(p => p.StudentId == studentId);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((searchParams.PageNumber - 1) * searchParams.PageSize)
            .Take(searchParams.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<IEnumerable<Payment>> GetByEnrollmentIdAsync(int enrollmentId)
    {
        var query = _context.Payments
            .AsQueryable()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(p => p.Enrollment)
                .ThenInclude(e => e.Student)
            .Include(p => p.Enrollment)
                .ThenInclude(e => e.Course)
            .Where(p => p.EnrollmentId == enrollmentId);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return items;
    }

    public async Task<(IEnumerable<Payment> Items, int TotalCount)> GetPagedAsync(PaginationParamsDto searchParams)
    {
        var query = _context.Payments
            .AsQueryable()
            .AsNoTracking()
            .Include(p => p.Enrollment)
                .ThenInclude(e => e.Course);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((searchParams.PageNumber - 1) * searchParams.PageSize)
            .Take(searchParams.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task AddAsync(Payment payment)
    {
        await _context.Payments.AddAsync(payment);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Payment payment)
    {
        _context.Entry(payment).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsActiveByEnrollmentAsync(int enrollmentId)
    {
        return await _context.Payments.AnyAsync(p => p.EnrollmentId == enrollmentId && p.IsActive);
    }

    public async Task DeleteAsync(Payment payment)
    {
        _context.Payments.Remove(payment);
        await _context.SaveChangesAsync();
    }
}
