using FluentValidation;

namespace TechCurse.Application.Features.Courses.Commands.UpdateCourse;

public class UpdateCourseCommandValidator : AbstractValidator<UpdateCourseCommand>
{
    public UpdateCourseCommandValidator()
    {
        RuleFor(c => c.Id)
            .GreaterThan(0).WithMessage("O Id do curso deve ser válido.");

        RuleFor(c => c.Titulo)
            .NotEmpty().WithMessage("O título é obrigatório.")
            .MaximumLength(100).WithMessage("O título deve ter no máximo 100 caracteres.");

        RuleFor(c => c.Descricao)
            .NotEmpty().WithMessage("A descrição é obrigatória.");

        RuleFor(c => c.Categoria)
            .NotEmpty().WithMessage("A categoria é obrigatória.")
            .MaximumLength(50).WithMessage("A categoria deve ter no máximo 50 caracteres.");

        RuleFor(c => c.CargaHoraria)
            .GreaterThan(0).WithMessage("A carga horária deve ser maior que zero.");
    }
}
