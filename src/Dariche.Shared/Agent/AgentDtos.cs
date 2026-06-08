namespace Dariche.Shared.Agent;

public sealed record AgentHeartbeatRequest(
    string AgentId,
    string Hostname,
    string Version,
    DateTimeOffset UtcNow,
    object? Metrics = null);

public sealed record AgentHeartbeatResponse(bool Ok, string Message, int PollIntervalSeconds);

public sealed record AgentJobEnvelope(
    Guid JobId,
    string Type,
    string PayloadJson,
    DateTimeOffset CreatedAtUtc,
    int Attempt);

public sealed record AgentJobResultRequest(
    string AgentId,
    Guid JobId,
    string Status,
    string? ResultJson,
    string? ErrorMessage);

public sealed record AgentJobResultResponse(bool Ok, string Message);
