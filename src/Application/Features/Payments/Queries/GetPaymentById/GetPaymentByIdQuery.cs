using MediatR;
using TechCurse.Application.DTOs;

namespace TechCurse.Application.Features.Payments.Queries.GetPaymentById;

public record GetPaymentByIdQuery(int Id) : IRequest<PaymentOutputDto>;
