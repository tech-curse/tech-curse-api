using MediatR;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Application.Features.Payments.Queries.GetPaymentsByEnrollmentId;

public class GetPaymentsByEnrollmentIdQueryHandler : IRequestHandler<GetPaymentsByEnrollmentIdQuery, IEnumerable<PaymentOutputDto>>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly ICacheService _cacheService;
    private readonly ICurrentUserService _currentUserService;

    public GetPaymentsByEnrollmentIdQueryHandler(IPaymentRepository paymentRepository, IEnrollmentRepository enrollmentRepository, IStudentRepository studentRepository, ICacheService cacheService, ICurrentUserService currentUserService)
    {
        _paymentRepository = paymentRepository;
        _enrollmentRepository = enrollmentRepository;
        _studentRepository = studentRepository;
        _cacheService = cacheService;
        _currentUserService = currentUserService;
    }

    public async Task<IEnumerable<PaymentOutputDto>> Handle(GetPaymentsByEnrollmentIdQuery request, CancellationToken cancellationToken)
    {
        var enrollmentId = request.EnrollmentId;
        var cacheKey = $"{ChavesDeCachePagamento.PorMatricula}{enrollmentId}";

        var enrollment = await _enrollmentRepository.GetByIdAsync(enrollmentId);
        var student = enrollment is null ? null : await _studentRepository.GetByIdIncludingDeletedAsync(enrollment.StudentId);
        if (student == null) throw new NotFoundException("Matrícula não encontrada.");

        await ValidateRoleAcess(student.IdentityUserId);

        var cached = await _cacheService.GetAsync<IEnumerable<PaymentOutputDto>>(cacheKey);
        if (cached != null) return cached;

        var payments = await _paymentRepository.GetByEnrollmentIdAsync(enrollmentId);

        var dtos = payments.Select(p => p.ParaDto()).ToList();

        await _cacheService.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(15));

        return dtos;
    }

    private Task ValidateRoleAcess(string targetIdentityUserId)
    {
        var currentUserId = _currentUserService.GetUserId();
        var isAdmin = _currentUserService.IsInRole(UserRole.Admin);

        if (currentUserId != targetIdentityUserId && !isAdmin)
            throw new ForbiddenAccessException("Você não possuí permissão suficiente para acessar este registro!");

        return Task.CompletedTask;
    }
}
