namespace Dariche.Commerce.Options;

public sealed class AgentOptions
{
    public string DefaultAgentId { get; set; } = "iran-main";
    public string DefaultAgentSecret { get; set; } = "CHANGE_ME_AGENT_SECRET";
    public int MaxJobAttempts { get; set; } = 3;
}
