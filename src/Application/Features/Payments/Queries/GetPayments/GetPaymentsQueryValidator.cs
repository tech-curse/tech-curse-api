using FluentValidation;
using TechCurse.Application.Common.Validation;

namespace TechCurse.Application.Features.Payments.Queries.GetPayments;

public class GetPaymentsQueryValidator : AbstractValidator<GetPaymentsQuery>
{
    public GetPaymentsQueryValidator()
    {
        RuleFor(q => q.SearchParams.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage(MensagensDePaginacao.PaginaInvalida)
            .OverridePropertyName("PageNumber");

        RuleFor(q => q.SearchParams.PageSize)
            .GreaterThanOrEqualTo(1).WithMessage(MensagensDePaginacao.TamanhoInvalido)
            .OverridePropertyName("PageSize");
    }
}
