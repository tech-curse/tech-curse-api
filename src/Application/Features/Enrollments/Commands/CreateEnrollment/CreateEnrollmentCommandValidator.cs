using FluentValidation;

namespace TechCurse.Application.Features.Enrollments.Commands.CreateEnrollment;

public class CreateEnrollmentCommandValidator : AbstractValidator<CreateEnrollmentCommand>
{
    public CreateEnrollmentCommandValidator()
    {
        RuleFor(c => c.CourseId)
            .GreaterThan(0).WithMessage("O ID do curso deve ser maior que zero.");

        RuleFor(c => c.StudentId)
            .GreaterThan(0).WithMessage("O ID do estudante deve ser maior que zero.");
    }
}
