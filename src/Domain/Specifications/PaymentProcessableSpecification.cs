namespace TechCurse.Domain.Specifications;

public interface ISpecification<T>
{
    bool IsSatisfiedBy(T entity);
    string ErrorMessage { get; }
}

public class PaymentProcessableSpecification : ISpecification<Entities.Payment>
{
    public string ErrorMessage => "O pagamento não está em um estado válido para processamento.";

    public bool IsSatisfiedBy(Entities.Payment payment)
    {
        return payment.IsActive && payment.Status == Enums.PaymentStatus.Pending;
    }
}
