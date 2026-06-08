namespace Dariche.Commerce.Domain;

public sealed class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long TelegramUserId { get; set; }
    public Guid OrderId { get; set; }
    public string AgentId { get; set; } = default!;
    public string ClientEmail { get; set; } = default!;
    public string ClientUuid { get; set; } = default!;
    public string SubId { get; set; } = default!;
    public string SubscriptionUrl { get; set; } = default!;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Pending;
    public int TrafficGb { get; set; }
    public long ConsumedBytes { get; set; } = 0;
    public string AssignedInboundTags { get; set; } = string.Empty; // تبدیل به string برای ذخیره JSON
    public DateTimeOffset ExpireAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    
    // Helper method for tags
    public string[] GetTags() => string.IsNullOrEmpty(AssignedInboundTags) 
        ? Array.Empty<string>() 
        : System.Text.Json.JsonSerializer.Deserialize<string[]>(AssignedInboundTags) ?? Array.Empty<string>();
        
    public void SetTags(string[] tags) => AssignedInboundTags = System.Text.Json.JsonSerializer.Serialize(tags);
}