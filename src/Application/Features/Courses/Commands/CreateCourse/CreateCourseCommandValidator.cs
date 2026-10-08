using FluentValidation;

namespace TechCurse.Application.Features.Courses.Commands.CreateCourse;

public class CreateCourseCommandValidator : AbstractValidator<CreateCourseCommand>
{
    public CreateCourseCommandValidator()
    {
        RuleFor(c => c.Titulo)
            .NotEmpty().WithMessage("O título é obrigatório.")
            .MaximumLength(100).WithMessage("O título deve ter no máximo 100 caracteres.");

        RuleFor(c => c.Descricao)
            .NotEmpty().WithMessage("A descrição é obrigatória.");

        RuleFor(c => c.Categoria)
            .NotEmpty().WithMessage("A categoria é obrigatória.");

        RuleFor(c => c.CargaHoraria)
            .GreaterThan(0).WithMessage("A carga horária deve ser maior que zero.");
    }
}
