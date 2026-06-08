namespace Dariche.IranAgent.Options;

public sealed class AgentOptions
{
    public string AgentId { get; set; } = "iran-main";
    public string AgentSecret { get; set; } = "";
    public string CommerceBaseUrl { get; set; } = "https://localhost:7001";
    public int PollIntervalSeconds { get; set; } = 5;
}

public sealed class XuiOptions
{
    public string DbPath { get; set; } = "/etc/x-ui/x-ui.db";
    public string SubscriptionBaseUrl { get; set; } = "https://landing.example.com/cli";
    public bool CreateBackupBeforeWrite { get; set; } = true;
    public string BackupDirectory { get; set; } = "/root/backups/dariche-agent";
}
