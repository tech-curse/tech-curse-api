using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Specifications;

namespace TechCurse.UnitTests.Pagamentos;

public sealed class PaymentProcessableSpecificationTests
{
    private readonly PaymentProcessableSpecification _especificacao = new();

    [Fact]
    [Trait("Especificacao", "PAG-008")]
    public void Pagamento_pendente_e_ativo_pode_ser_processado()
    {
        var pagamento = new Payment { Status = PaymentStatus.Pending, IsActive = true };

        Assert.True(_especificacao.IsSatisfiedBy(pagamento));
    }

    [Theory]
    [Trait("Especificacao", "PAG-008")]
    [InlineData(PaymentStatus.Paid)]
    [InlineData(PaymentStatus.Refunded)]
    [InlineData(PaymentStatus.Failed)]
    public void Pagamento_fora_de_Pending_nao_pode_ser_processado(PaymentStatus status)
    {
        var pagamento = new Payment { Status = status, IsActive = true };

        Assert.False(_especificacao.IsSatisfiedBy(pagamento));
    }

    [Fact]
    [Trait("Especificacao", "PAG-008")]
    public void Pagamento_inativo_nao_pode_ser_processado()
    {
        var pagamento = new Payment { Status = PaymentStatus.Pending, IsActive = false };

        Assert.False(_especificacao.IsSatisfiedBy(pagamento));
    }

    [Fact]
    [Trait("Especificacao", "PAG-008")]
    public void Mensagem_de_erro_e_a_da_especificacao()
    {
        Assert.Equal("O pagamento não está em um estado válido para processamento.", _especificacao.ErrorMessage);
    }
}
