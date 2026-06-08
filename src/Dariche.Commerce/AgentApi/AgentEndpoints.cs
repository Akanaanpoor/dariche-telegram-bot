using System.Text.Json;
using Dariche.Commerce.Data;
using Dariche.Commerce.Domain;
using Dariche.Shared.Agent;
using Microsoft.EntityFrameworkCore;

namespace Dariche.Commerce.AgentApi;

public static class AgentEndpoints
{
    public static RouteGroupBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/agent");

        g.MapPost("/heartbeat", async (HttpRequest req, CommerceDbContext db, AgentHeartbeatRequest body, CancellationToken ct) =>
        {
            var auth = await AuthorizeAsync(req, db, ct);
            if (!auth.Ok) return Results.Unauthorized();
            var agent = auth.Agent!;
            agent.Hostname = body.Hostname;
            agent.Version = body.Version;
            agent.LastSeenUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new AgentHeartbeatResponse(true, "ok", 5));
        });

        g.MapGet("/jobs/next", async (HttpRequest req, CommerceDbContext db, CancellationToken ct) =>
        {
            var auth = await AuthorizeAsync(req, db, ct);
            if (!auth.Ok) return Results.Unauthorized();
            var agentId = auth.Agent!.AgentId;
            var job = await db.ProvisioningJobs
                .Where(x => x.AgentId == agentId && x.Status == ProvisioningJobStatus.Pending)
                .OrderBy(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);
            if (job is null) return Results.NoContent();
            job.Status = ProvisioningJobStatus.Picked;
            job.Attempt += 1;
            job.PickedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new AgentJobEnvelope(job.Id, job.Type, job.PayloadJson, job.CreatedAtUtc, job.Attempt));
        });

        g.MapPost("/jobs/{jobId:guid}/result", async (Guid jobId, HttpRequest req, CommerceDbContext db, AgentJobResultRequest body, CancellationToken ct) =>
        {
            var auth = await AuthorizeAsync(req, db, ct);
            if (!auth.Ok) return Results.Unauthorized();
            var job = await db.ProvisioningJobs.FirstOrDefaultAsync(x => x.Id == jobId && x.AgentId == auth.Agent!.AgentId, ct);
            if (job is null) return Results.NotFound(new AgentJobResultResponse(false, "job not found"));
            job.ResultJson = body.ResultJson;
            job.ErrorMessage = body.ErrorMessage;
            job.FinishedAtUtc = DateTimeOffset.UtcNow;
            job.Status = string.Equals(body.Status, "Succeeded", StringComparison.OrdinalIgnoreCase) ? ProvisioningJobStatus.Succeeded : ProvisioningJobStatus.Failed;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new AgentJobResultResponse(true, "accepted"));
        });

        return g;
    }

    private static async Task<(bool Ok, AgentNode? Agent)> AuthorizeAsync(HttpRequest req, CommerceDbContext db, CancellationToken ct)
    {
        if (!req.Headers.TryGetValue("X-Dariche-Agent-Id", out var agentIdValues)) return (false, null);
        if (!req.Headers.TryGetValue("X-Dariche-Timestamp", out var tsValues)) return (false, null);
        if (!req.Headers.TryGetValue("X-Dariche-Nonce", out var nonceValues)) return (false, null);
        if (!req.Headers.TryGetValue("X-Dariche-Signature", out var sigValues)) return (false, null);

        var agentId = agentIdValues.ToString();
        var timestamp = tsValues.ToString();
        var nonce = nonceValues.ToString();
        var signature = sigValues.ToString();
        if (!long.TryParse(timestamp, out var unix)) return (false, null);
        var age = Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - unix);
        if (age > 300) return (false, null);

        var agent = await db.Agents.FirstOrDefaultAsync(x => x.AgentId == agentId && x.IsEnabled, ct);
        if (agent is null) return (false, null);

        req.EnableBuffering();
        using var sr = new StreamReader(req.Body, leaveOpen: true);
        var body = await sr.ReadToEndAsync(ct);
        req.Body.Position = 0;
        var expected = HmacSigner.ComputeSignature(agent.Secret, req.Method, req.Path + req.QueryString, timestamp, nonce, body);
        return HmacSigner.FixedTimeEquals(expected, signature) ? (true, agent) : (false, null);
    }
}
