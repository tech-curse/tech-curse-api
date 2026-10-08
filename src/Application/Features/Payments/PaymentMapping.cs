using TechCurse.Application.DTOs;
using TechCurse.Domain.Entities;

namespace TechCurse.Application.Features.Payments;

public static class PaymentMapping
{
    public static PaymentOutputDto ParaDto(this Payment payment)
    {
        return new PaymentOutputDto(
            payment.PaymentId,
            payment.EnrollmentId,
            payment.StudentId,
            payment.Amount,
            payment.Status,
            payment.IsActive,
            payment.CreatedAt,
            payment.PaidAt,
            payment.ExternalTransactionId,
            payment.Enrollment.CourseId,
            payment.Enrollment.Course.Titulo);
    }
}
