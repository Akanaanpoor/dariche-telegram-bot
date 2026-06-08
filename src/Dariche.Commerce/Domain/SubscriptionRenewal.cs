namespace Dariche.Commerce.Domain;

public class SubscriptionRenewal
{
    public Guid Id { get; set; }

    public Guid SubscriptionId { get; set; }

    public Guid OrderId { get; set; }

    public int AddedDays { get; set; }

    public int AddedTrafficGb { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}