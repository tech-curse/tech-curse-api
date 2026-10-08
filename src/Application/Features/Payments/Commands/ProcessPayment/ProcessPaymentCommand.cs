using MediatR;
using TechCurse.Application.DTOs;
using TechCurse.Domain.Enums;

namespace TechCurse.Application.Features.Payments.Commands.ProcessPayment;

public record ProcessPaymentCommand(int PaymentId, PaymentMethodType Type, string IdempotencyKey) : IRequest<ProcessPaymentOutputDto>;
