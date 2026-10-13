using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;

namespace TechCurse.Api.IntegrationTests.Infraestrutura;

public sealed class GatewayQueEsgotaOTempo : IPaymentGatewayAdapter
{
    private static Task<GatewayResponse> TempoEsgotado() => Task.FromException<GatewayResponse>(new OperationCanceledException());

    public Task<GatewayResponse> CreateTransactionAsync(decimal amount, string idempotencyKey, CancellationToken cancellationToken) => TempoEsgotado();

    public Task<GatewayResponse> ConfirmTransactionAsync(string ExternalTransactionId, CancellationToken cancellationToken) => TempoEsgotado();

    public Task<GatewayResponse> RefundTransactionAsync(string ExternalTransactionId, CancellationToken cancellationToken, string idempotencyKey) => TempoEsgotado();
}
