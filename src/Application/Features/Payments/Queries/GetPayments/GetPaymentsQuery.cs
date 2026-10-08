using MediatR;
using TechCurse.Application.DTOs;

namespace TechCurse.Application.Features.Payments.Queries.GetPayments;

public record GetPaymentsQuery(PaginationParamsDto SearchParams) : IRequest<PagedResultDto<PaymentOutputDto>>;
