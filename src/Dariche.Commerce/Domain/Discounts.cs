using Dariche.Commerce.Domain;

public sealed class Discount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }
    public int MaxUsage { get; set; }
    public int CurrentUsage { get; set; }
    public DateTimeOffset? ExpireAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
}