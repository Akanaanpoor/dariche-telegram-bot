namespace Dariche.Commerce.Domain;

public sealed class Payment
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public PaymentProvider Provider { get; set; }

    public decimal Amount { get; set; }

    public string? RefNumber { get; set; }

    public string? TrackingCode { get; set; }

    public PaymentStatus Status { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? PaidAtUtc { get; set; }
}