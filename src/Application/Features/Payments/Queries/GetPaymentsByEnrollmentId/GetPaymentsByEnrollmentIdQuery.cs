using MediatR;
using TechCurse.Application.DTOs;

namespace TechCurse.Application.Features.Payments.Queries.GetPaymentsByEnrollmentId;

public record GetPaymentsByEnrollmentIdQuery(int EnrollmentId) : IRequest<IEnumerable<PaymentOutputDto>>;
