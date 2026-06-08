namespace Dariche.Commerce.Domain;

public sealed class ProvisioningJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TargetAgentId { get; set; } = default!;
    public Guid? OrderId { get; set; }
    public Guid? SubscriptionId { get; set; }
    public ProvisioningJobType Type { get; set; }
    public ProvisioningJobStatus Status { get; set; } = ProvisioningJobStatus.Pending;
    public string PayloadJson { get; set; } = default!;
    public string? ResultJson { get; set; }
    public string? ErrorMessage { get; set; }
    public int Attempt { get; set; } = 0;
    public bool UserNotified { get; set; } = false;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PickedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    
    // Navigation properties
    public Order? Order { get; set; }
}