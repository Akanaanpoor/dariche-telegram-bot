namespace Dariche.Shared.Provisioning;

public static class ProvisioningJobTypes
{
    public const string CreateClient = "CreateClient";
    public const string RenewClient = "RenewClient";
    public const string DisableClient = "DisableClient";
}

public sealed record CreateClientCommand(
    Guid OrderId,
    long TelegramUserId,
    string PlanCode,
    int DurationDays,
    long TrafficGb,
    string[] AssignedInboundTags,
    string ClientEmail,
    string ClientLabel);

public sealed record CreateClientResult(
    string ClientUuid,
    string ClientEmail,
    string SubId,
    string SubscriptionUrl,
    string[] AssignedInboundTags,
    DateTimeOffset ExpireAtUtc,
    long TrafficGb);

public sealed record RenewClientCommand(
    string ClientEmail,
    int AdditionalDays,
    string[] AssignedInboundTags);

public sealed record RenewClientResult(
    string ClientEmail,
    DateTimeOffset ExpireAtUtc);

public sealed record DisableClientCommand(string ClientEmail, string[] AssignedInboundTags);
public sealed record DisableClientResult(string ClientEmail, bool Disabled);
