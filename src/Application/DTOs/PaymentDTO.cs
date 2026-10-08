using System.ComponentModel.DataAnnotations;
using TechCurse.Domain.Enums;

namespace TechCurse.Application.DTOs;

public record PaymentOutputDto(int PaymentId, int EnrollmentId, int StudentId, decimal Amount, PaymentStatus Status, bool IsActive, DateTime CreatedAt, DateTime? PaidAt, string? ExternalTransactionId, int CourseId, string CourseTitulo);

public record CreatePaymentDto([Required] int EnrollmentId, [Required] decimal Amount);
public record ProcessPaymentDto(int PaymentId, [Required] PaymentMethodType type);
public record RefundPaymentDto(int PaymentId, string? Reason);
public record GatewayResponse(bool IsSuccess, string? TransactionId, string? ReceiptUrl, string? ErrorCode, string? ErrorMessage, DateTime ProcessedAt);
public record ProcessPaymentOutputDto(bool Success, string Message, string? ExternalTransactionId);
public record RefundPaymentOutputDto(bool Success, string Message);
