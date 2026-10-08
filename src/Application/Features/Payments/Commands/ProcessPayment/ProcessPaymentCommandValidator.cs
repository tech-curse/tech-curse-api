using FluentValidation;

namespace TechCurse.Application.Features.Payments.Commands.ProcessPayment;

public class ProcessPaymentCommandValidator : AbstractValidator<ProcessPaymentCommand>
{
    public ProcessPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).GreaterThan(0).WithMessage("O ID do pagamento deve ser maior que zero.");
        RuleFor(x => x.Type).IsInEnum().WithMessage("Tipo de pagamento inválido.");
        RuleFor(x => x.IdempotencyKey).NotEmpty().WithMessage("A chave de idempotência é obrigatória.");
    }
}
