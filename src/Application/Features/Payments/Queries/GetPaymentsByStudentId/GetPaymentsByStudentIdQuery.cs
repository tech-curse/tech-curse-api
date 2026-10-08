using MediatR;
using TechCurse.Application.DTOs;

namespace TechCurse.Application.Features.Payments.Queries.GetPaymentsByStudentId;

public record GetPaymentsByStudentIdQuery(int StudentId, PaginationParamsDto SearchParams) : IRequest<PagedResultDto<PaymentOutputDto>>;
