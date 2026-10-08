using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;

namespace TechCurse.Application.Strategies;

public class CreditCardPaymentStrategy : IPaymentStrategy
{
    public PaymentMethodType PaymentMethodType => PaymentMethodType.CreditCard;

    public async Task<GatewayResponse> ProcessAsync(Payment payment, IPaymentGatewayAdapter gateway, string idempotencyKey, CancellationToken cancellationToken)
    {
        return await gateway.CreateTransactionAsync(payment.Amount, idempotencyKey, cancellationToken);
    }
}
