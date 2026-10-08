using MediatR;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;

namespace TechCurse.Application.Features.Payments.Queries.GetPayments;

public class GetPaymentsQueryHandler : IRequestHandler<GetPaymentsQuery, PagedResultDto<PaymentOutputDto>>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ICacheService _cacheService;

    public GetPaymentsQueryHandler(IPaymentRepository paymentRepository, ICacheService cacheService)
    {
        _paymentRepository = paymentRepository;
        _cacheService = cacheService;
    }

    public async Task<PagedResultDto<PaymentOutputDto>> Handle(GetPaymentsQuery request, CancellationToken cancellationToken)
    {
        var searchParams = request.SearchParams;
        var cacheKey = $"{ChavesDeCachePagamento.Lista}page:{searchParams.PageNumber}:size:{searchParams.PageSize}:sort:{searchParams.SortBy}_{searchParams.SortDirection}";

        var cachedResult = await _cacheService.GetAsync<PagedResultDto<PaymentOutputDto>>(cacheKey);
        if (cachedResult != null) return cachedResult;

        var (items, totalCount) = await _paymentRepository.GetPagedAsync(searchParams);

        var dtos = items.Select(p => p.ParaDto());

        var result = new PagedResultDto<PaymentOutputDto>(dtos, totalCount, searchParams.PageNumber, searchParams.PageSize);
        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(15));

        return result;
    }
}
