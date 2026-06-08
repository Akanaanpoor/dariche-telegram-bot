namespace Dariche.Commerce.Domain;

public sealed class Order
{
    public Guid Id { get; set; }

    public Guid TelegramUserId { get; set; }

    public Guid PlanId { get; set; }

    public Guid? DiscountId { get; set; }

    public decimal Amount { get; set; }

    public decimal FinalAmount { get; set; }

    public OrderStatus Status { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

}
