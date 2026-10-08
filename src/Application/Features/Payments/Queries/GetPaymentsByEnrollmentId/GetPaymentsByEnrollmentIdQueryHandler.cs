using MediatR;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Application.Features.Payments.Queries.GetPaymentsByEnrollmentId;

public class GetPaymentsByEnrollmentIdQueryHandler : IRequestHandler<GetPaymentsByEnrollmentIdQuery, IEnumerable<PaymentOutputDto>>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ICacheService _cacheService;
    private readonly ICurrentUserService _currentUserService;

    public GetPaymentsByEnrollmentIdQueryHandler(IPaymentRepository paymentRepository, ICacheService cacheService, ICurrentUserService currentUserService)
    {
        _paymentRepository = paymentRepository;
        _cacheService = cacheService;
        _currentUserService = currentUserService;
    }

    public async Task<IEnumerable<PaymentOutputDto>> Handle(GetPaymentsByEnrollmentIdQuery request, CancellationToken cancellationToken)
    {
        var enrollmentId = request.EnrollmentId;
        var cacheKey = $"{ChavesDeCachePagamento.PorMatricula}{enrollmentId}";

        var payments = await _paymentRepository.GetByEnrollmentIdAsync(enrollmentId);

        var student = payments.FirstOrDefault()?.Enrollment.Student;
        if (student == null) throw new NotFoundException("Matrícula não encontrada ou sem estudante associado.");

        await ValidateRoleAcess(student.IdentityUserId);

        var cached = await _cacheService.GetAsync<IEnumerable<PaymentOutputDto>>(cacheKey);
        if (cached != null) return cached;

        var dtos = payments.Select(p => p.ParaDto()).ToList();

        await _cacheService.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(15));

        return dtos;
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
