using MediatR;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Application.Features.Payments.Queries.GetPaymentsByStudentId;

public class GetPaymentsByStudentIdQueryHandler : IRequestHandler<GetPaymentsByStudentIdQuery, PagedResultDto<PaymentOutputDto>>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly ICacheService _cacheService;
    private readonly ICurrentUserService _currentUserService;

    public GetPaymentsByStudentIdQueryHandler(IPaymentRepository paymentRepository, IStudentRepository studentRepository, ICacheService cacheService, ICurrentUserService currentUserService)
    {
        _paymentRepository = paymentRepository;
        _studentRepository = studentRepository;
        _cacheService = cacheService;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResultDto<PaymentOutputDto>> Handle(GetPaymentsByStudentIdQuery request, CancellationToken cancellationToken)
    {
        var studentId = request.StudentId;
        var searchParams = request.SearchParams;
        var cacheKey = $"{ChavesDeCachePagamento.PorEstudante}id:{studentId}:page:{searchParams.PageNumber}:size:{searchParams.PageSize}:sort:{searchParams.SortBy}_{searchParams.SortDirection}";

        var student = await _studentRepository.GetByIdAsync(studentId);
        if (student == null) throw new NotFoundException("Estudante não encontrado.");

        await ValidateRoleAcess(student.IdentityUserId);

        var cached = await _cacheService.GetAsync<PagedResultDto<PaymentOutputDto>>(cacheKey);
        if (cached != null) return cached;

        var (items, totalCount) = await _paymentRepository.GetByStudentIdAsync(studentId, searchParams);

        var dtos = items.Select(p => p.ParaDto());

        var result = new PagedResultDto<PaymentOutputDto>(dtos, totalCount, searchParams.PageNumber, searchParams.PageSize);
        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(15));

        return result;
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
