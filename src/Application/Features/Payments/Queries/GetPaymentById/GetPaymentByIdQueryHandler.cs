using MediatR;
using TechCurse.Application.DTOs;
using TechCurse.Application.Features.Payments;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Application.Features.Payments.Queries.GetPaymentById;

public class GetPaymentByIdQueryHandler : IRequestHandler<GetPaymentByIdQuery, PaymentOutputDto>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ICacheService _cacheService;
    private readonly ICurrentUserService _currentUserService;

    public GetPaymentByIdQueryHandler(IPaymentRepository paymentRepository, ICacheService cacheService, ICurrentUserService currentUserService)
    {
        _paymentRepository = paymentRepository;
        _cacheService = cacheService;
        _currentUserService = currentUserService;
    }

    public async Task<PaymentOutputDto> Handle(GetPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"{ChavesDeCachePagamento.Item}{request.Id}";

        var payment = await _paymentRepository.GetByIdAsync(request.Id);
        if (payment == null) throw new NotFoundException("Pagamento não encontrado.");

        await ValidateRoleAcess(payment.Student.IdentityUserId);

        var cached = await _cacheService.GetAsync<PaymentOutputDto>(cacheKey);
        if (cached != null) return cached;

        var dto = payment.ParaDto();

        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(15));
        return dto;
    }

    private Task ValidateRoleAcess(string targetIdentityUserId)
    {
        var currentUserId = _currentUserService.GetUserId();
        var isAdmin = _currentUserService.IsInRole(UserRole.Admin);

        if (currentUserId != targetIdentityUserId && !isAdmin)
            throw new NotAllowedException("Você não possuí permissão suficiente para acessar este registro!");

        return Task.CompletedTask;
    }
}
