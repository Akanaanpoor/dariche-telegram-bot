using System.Text.Json;

namespace Dariche.Commerce.Domain;

public sealed class Subscription
{
    public Guid Id { get; set; }

    public Guid TelegramUserId { get; set; }

    public Guid OrderId { get; set; }

    public Guid AgentId { get; set; }

    public string ClientEmail { get; set; }

    public string ClientUuid { get; set; }

    public string SubId { get; set; }

    public string SubscriptionUrl { get; set; }

    public SubscriptionStatus Status { get; set; }

    public int TrafficGb { get; set; }

    public long ConsumedBytes { get; set; }

    public DateTimeOffset ExpireAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    
}