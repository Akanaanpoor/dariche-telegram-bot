namespace Dariche.Shared.Agent;

// Heartbeat Request - ساده شده
public sealed record AgentHeartbeatRequest(
    string Hostname,
    string Version
);

public sealed record AgentHeartbeatResponse(
    bool Ok,
    string Message,
    int IntervalSeconds
);

// Job Envelope
public sealed record AgentJobEnvelope(
    Guid JobId,
    string Type,
    string PayloadJson,
    DateTimeOffset CreatedAt,
    int Attempt
);

// Job Result Request - با Guid
public sealed record AgentJobResultRequest(
    string Status,
    string? ResultJson,
    string? ErrorMessage
);

// Job Result Response
public sealed record AgentJobResultResponse(
    bool Accepted,
    string Message
);