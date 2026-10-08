using FluentValidation;

namespace TechCurse.Application.Features.Payments.Commands.RefundPayment;

public class RefundPaymentCommandValidator : AbstractValidator<RefundPaymentCommand>
{
    public RefundPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).GreaterThan(0).WithMessage("O ID do pagamento deve ser maior que zero.");
        RuleFor(x => x.IdempotencyKey).NotEmpty().WithMessage("A chave de idempotência é obrigatória.");
    }
}
