namespace Dariche.Shared.Provisioning;

public sealed record CreateClientCommand(
    Guid OrderId,
    long TelegramUserId,
    string PlanCode,
    int DurationDays,
    int TrafficGb,
    string[] InboundTags,
    string ClientEmail,
    string SubId,
    string Remark,
    string ClientLabel,  // اضافه شد
    string[] AssignedInboundTags  // اضافه شد
);

public sealed record CreateClientResult(
    string ClientEmail,
    string ClientUuid,
    string SubId,
    string SubscriptionUrl,
    DateTimeOffset ExpireAtUtc,
    int TrafficGb,
    string[] AssignedInboundTags,
    (string Name, object? Value)[]? Extra  // اضافه شد
);