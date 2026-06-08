namespace Dariche.Commerce.Domain;

public sealed class AgentNode
{
    public string AgentId { get; set; } = default!;
    public string Secret { get; set; } = default!;
    public string? Hostname { get; set; }
    public string? Version { get; set; }
    public DateTimeOffset? LastSeenUtc { get; set; }
    public bool IsEnabled { get; set; } = true;
}