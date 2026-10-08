using FluentValidation;

namespace TechCurse.Application.Features.Payments.Commands.CreatePayment;

public class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.EnrollmentId).GreaterThan(0).WithMessage("O ID da matrícula deve ser maior que zero.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("O valor do pagamento deve ser maior que zero.");
    }
}
