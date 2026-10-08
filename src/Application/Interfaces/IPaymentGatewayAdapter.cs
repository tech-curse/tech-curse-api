using TechCurse.Application.DTOs;
using TechCurse.Domain.Entities;

namespace TechCurse.Application.Interfaces;

public interface IPaymentGatewayAdapter
{
    Task<GatewayResponse> CreateTransactionAsync(decimal amount, string idempotencyKey, CancellationToken cancellationToken);
    Task<GatewayResponse> ConfirmTransactionAsync(string ExternalTransactionId, CancellationToken cancellationToken);
    Task<GatewayResponse> RefundTransactionAsync(string ExternalTransactionId, CancellationToken cancellationToken, string idempotencyKey);
}
