using MediatR;
using Microsoft.Extensions.Logging;
using TechCurse.Application.DTOs;
using TechCurse.Application.Features.Payments;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Application.Features.Payments.Commands.CreatePayment;

public class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, PaymentOutputDto>
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CreatePaymentCommandHandler> _logger;

    public CreatePaymentCommandHandler(IEnrollmentRepository enrollmentRepository, IPaymentRepository paymentRepository, ICacheService cacheService, ILogger<CreatePaymentCommandHandler> logger)
    {
        _enrollmentRepository = enrollmentRepository;
        _paymentRepository = paymentRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<PaymentOutputDto> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        Enrollment? enrollment = await _enrollmentRepository.GetByIdAsync(request.EnrollmentId);
        if (enrollment == null)
        {
            _logger.LogWarning("Tentativa de criar pagamento para matrícula inexistente: {EnrollmentId}", request.EnrollmentId);
            throw new NotFoundException("Matrícula não encontrada.");
        }

        var isEnrollmentActive = await _enrollmentRepository.EnrollmentIsActiveAsync(request.EnrollmentId);
        if (!isEnrollmentActive)
        {
            _logger.LogWarning("Falha ao criar intenção. Matrícula inativa: {EnrollmentId}", request.EnrollmentId);
            throw new NotAllowedException("Não é possível criar um pagamento para uma matrícula inativa.");
        }

        var paymentExists = await _paymentRepository.ExistsActiveByEnrollmentAsync(request.EnrollmentId);
        if (paymentExists)
        {
            throw new ConflictException("Já existe um pagamento ativo para esta matrícula.");
        }

        Payment newPayment = new Payment
        {
            EnrollmentId = request.EnrollmentId,
            StudentId = enrollment.StudentId,
            Amount = request.Amount,
            Status = PaymentStatus.Pending,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Enrollment = enrollment
        };

        await _paymentRepository.AddAsync(newPayment);
        await ClearPaymentCachesAsync();

        _logger.LogInformation("Intenção de pagamento criada com sucesso. PaymentId: {PaymentId}, EnrollmentId: {EnrollmentId}",
            newPayment.PaymentId, newPayment.EnrollmentId);

        return newPayment.ParaDto();
    }

    private async Task ClearPaymentCachesAsync()
    {
        await _cacheService.RemoveByPrefixAsync(ChavesDeCachePagamento.Lista);
        await _cacheService.RemoveByPrefixAsync(ChavesDeCachePagamento.Item);
        await _cacheService.RemoveByPrefixAsync(ChavesDeCachePagamento.PorEstudante);
        await _cacheService.RemoveByPrefixAsync(ChavesDeCachePagamento.PorMatricula);
    }
}
