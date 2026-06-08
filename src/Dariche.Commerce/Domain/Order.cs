namespace Dariche.Commerce.Domain;

public sealed class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long TelegramUserId { get; set; }
    public Guid PlanId { get; set; }
    public Guid? DiscountId { get; set; }
    public decimal AmountToman { get; set; }
    public decimal? FinalAmountToman { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.PendingPayment;
    public string? UserReceiptText { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PaidAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    
    // Navigation properties
    public TelegramUser? TelegramUser { get; set; }
    public Plan? Plan { get; set; }
}