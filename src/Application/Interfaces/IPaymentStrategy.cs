using TechCurse.Application.DTOs;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;

namespace TechCurse.Application.Interfaces;

public interface IPaymentStrategy
{
    PaymentMethodType PaymentMethodType { get; }
    Task<GatewayResponse> ProcessAsync(Payment payment, IPaymentGatewayAdapter gateway, string idempotencyKey, CancellationToken cancellationToken);
}
