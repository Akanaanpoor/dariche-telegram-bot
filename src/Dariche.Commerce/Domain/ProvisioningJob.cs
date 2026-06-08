namespace Dariche.Commerce.Domain;

public sealed class ProvisioningJob
{
    public Guid Id { get; set; }

    public Guid AgentId { get; set; }

    public Guid? OrderId { get; set; }

    public Guid? SubscriptionId { get; set; }

    public ProvisioningJobType Type { get; set; }

    public ProvisioningJobStatus Status { get; set; }

    public string PayloadJson { get; set; }

    public string? ResultJson { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? FinishedAtUtc { get; set; }
}