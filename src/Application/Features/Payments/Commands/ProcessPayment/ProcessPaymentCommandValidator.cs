using FluentValidation;
using TechCurse.Application.Factory;

namespace TechCurse.Application.Features.Payments.Commands.ProcessPayment;

public class ProcessPaymentCommandValidator : AbstractValidator<ProcessPaymentCommand>
{
    public const string MetodoNaoSuportado = "Método de pagamento não suportado.";

    public ProcessPaymentCommandValidator(PaymentStrategyFactory estrategias)
    {
        RuleFor(x => x.PaymentId).GreaterThan(0).WithMessage("O ID do pagamento deve ser maior que zero.");
        RuleFor(x => x.Type).Cascade(CascadeMode.Stop)
            .IsInEnum().WithMessage("Tipo de pagamento inválido.")
            .Must(estrategias.Suporta).WithMessage(MetodoNaoSuportado);
        RuleFor(x => x.IdempotencyKey).NotEmpty().WithMessage("A chave de idempotência é obrigatória.");
    }
}
