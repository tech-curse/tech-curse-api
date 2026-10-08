using TechCurse.Domain.Enums;

namespace TechCurse.Domain.Entities;

public class Payment
{
    public int PaymentId { get; set; }

    public int EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public decimal Amount { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public DateTime? RefundedAt { get; set; }

    public string? ExternalTransactionId { get; set; }
    public string? ReceiptUrl { get; set; }
}
