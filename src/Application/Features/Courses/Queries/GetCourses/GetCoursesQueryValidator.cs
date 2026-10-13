using FluentValidation;
using TechCurse.Application.Common.Validation;

namespace TechCurse.Application.Features.Courses.Queries.GetCourses;

public class GetCoursesQueryValidator : AbstractValidator<GetCoursesQuery>
{
    public GetCoursesQueryValidator()
    {
        RuleFor(q => q.SearchParams.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage(MensagensDePaginacao.PaginaInvalida)
            .OverridePropertyName("PageNumber");

        RuleFor(q => q.SearchParams.PageSize)
            .GreaterThanOrEqualTo(1).WithMessage(MensagensDePaginacao.TamanhoInvalido)
            .OverridePropertyName("PageSize");
    }
}
