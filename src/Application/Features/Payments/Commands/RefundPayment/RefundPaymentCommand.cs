using MediatR;
using TechCurse.Application.DTOs;

namespace TechCurse.Application.Features.Payments.Commands.RefundPayment;

public record RefundPaymentCommand(int PaymentId, string IdempotencyKey) : IRequest<RefundPaymentOutputDto>;
