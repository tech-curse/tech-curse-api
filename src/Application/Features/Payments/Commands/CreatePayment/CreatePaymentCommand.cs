using MediatR;
using TechCurse.Application.DTOs;

namespace TechCurse.Application.Features.Payments.Commands.CreatePayment;

public record CreatePaymentCommand(int EnrollmentId, decimal Amount) : IRequest<PaymentOutputDto>;
