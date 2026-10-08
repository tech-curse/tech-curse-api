using MediatR;
using Microsoft.Extensions.Logging;
using TechCurse.Application.DTOs;
using TechCurse.Application.Factory;
using TechCurse.Application.Features.Payments;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Exceptions;
using TechCurse.Domain.Specifications;

namespace TechCurse.Application.Features.Payments.Commands.ProcessPayment;

public class ProcessPaymentCommandHandler : IRequestHandler<ProcessPaymentCommand, ProcessPaymentOutputDto>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentGatewayAdapter _paymentGateway;
    private readonly PaymentStrategyFactory _strategyFactory;
    private readonly ICacheService _cacheService;
    private readonly ILogger<ProcessPaymentCommandHandler> _logger;

    public ProcessPaymentCommandHandler(
        IPaymentRepository paymentRepository,
        IPaymentGatewayAdapter paymentGateway,
        PaymentStrategyFactory strategyFactory,
        ICacheService cacheService,
        ILogger<ProcessPaymentCommandHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _paymentGateway = paymentGateway;
        _strategyFactory = strategyFactory;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<ProcessPaymentOutputDto> Handle(ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByIdAsync(request.PaymentId);
        if (payment == null) throw new NotFoundException("Pagamento não encontrado.");

        var processableSpec = new PaymentProcessableSpecification();
        if (!processableSpec.IsSatisfiedBy(payment))
        {
            throw new NotAllowedException(processableSpec.ErrorMessage);
        }

        var strategy = _strategyFactory.GetStrategy(request.Type);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var gatewayResult = await strategy.ProcessAsync(payment, _paymentGateway, request.IdempotencyKey, cts.Token);

            if (!gatewayResult.IsSuccess)
            {
                _logger.LogWarning("Falha no gateway ao processar PaymentId {PaymentId}. Erro: {ErrorCode} - {ErrorMessage}",
                    payment.PaymentId, gatewayResult.ErrorCode, gatewayResult.ErrorMessage);
                throw new BadRequestException($"Falha ao processar pagamento [{gatewayResult.ErrorCode}]: {gatewayResult.ErrorMessage}");
            }

            payment.Status = PaymentStatus.Paid;
            payment.PaidAt = gatewayResult.ProcessedAt;
            payment.ExternalTransactionId = gatewayResult.TransactionId;
            payment.ReceiptUrl = gatewayResult.ReceiptUrl;

            await _paymentRepository.UpdateAsync(payment);
            await ClearPaymentCachesAsync();

            _logger.LogInformation("Pagamento {PaymentId} processado com sucesso. TransactionId: {TransactionId}",
                payment.PaymentId, payment.ExternalTransactionId);

            return new ProcessPaymentOutputDto(true, "Pagamento processado com sucesso.", payment.ExternalTransactionId);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Timeout na comunicação com o gateway para o PaymentId {PaymentId}", request.PaymentId);
            throw new GatewayTimeoutException("A comunicação com o provedor de pagamento excedeu o tempo limite.");
        }
    }

    private async Task ClearPaymentCachesAsync()
    {
        await _cacheService.RemoveByPrefixAsync(ChavesDeCachePagamento.Lista);
        await _cacheService.RemoveByPrefixAsync(ChavesDeCachePagamento.Item);
        await _cacheService.RemoveByPrefixAsync(ChavesDeCachePagamento.PorEstudante);
        await _cacheService.RemoveByPrefixAsync(ChavesDeCachePagamento.PorMatricula);
    }
}
