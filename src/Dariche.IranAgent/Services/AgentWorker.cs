using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dariche.IranAgent.Options;
using Dariche.IranAgent.Xui;
using Dariche.Shared.Agent;
using Dariche.Shared.Provisioning;
using Microsoft.Extensions.Options;

namespace Dariche.IranAgent.Services;

public sealed class AgentWorker : BackgroundService
{
    private readonly SignedCommerceClient _client;
    private readonly XuiSqliteProvisioner _xui;
    private readonly AgentOptions _options;
    private readonly ILogger<AgentWorker> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public AgentWorker(SignedCommerceClient client, XuiSqliteProvisioner xui, IOptions<AgentOptions> options, ILogger<AgentWorker> logger)
    {
        _client = client; _xui = xui; _options = options.Value; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await HeartbeatAsync(stoppingToken);
                await PollOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent loop failed");
            }
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds)), stoppingToken);
        }
    }

    private async Task HeartbeatAsync(CancellationToken ct)
    {
        var req = new AgentHeartbeatRequest(_options.AgentId, Environment.MachineName, "0.1.0", DateTimeOffset.UtcNow);
        using var resp = await _client.PostSignedAsync("api/agent/heartbeat", req, ct);
        if (!resp.IsSuccessStatusCode) _logger.LogWarning("Heartbeat failed: {Status}", resp.StatusCode);
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        using var resp = await _client.GetSignedAsync("api/agent/jobs/next", ct);
        if (resp.StatusCode == HttpStatusCode.NoContent) return;
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Get job failed: {Status}", resp.StatusCode);
            return;
        }
        var job = await resp.Content.ReadFromJsonAsync<AgentJobEnvelope>(_json, ct);
        if (job is null) return;
        _logger.LogInformation("Picked job {JobId} type {Type}", job.JobId, job.Type);
        await ExecuteJobAsync(job, ct);
    }

    private async Task ExecuteJobAsync(AgentJobEnvelope job, CancellationToken ct)
    {
        try
        {
            object result = job.Type switch
            {
                ProvisioningJobTypes.CreateClient => await _xui.CreateClientAsync(JsonSerializer.Deserialize<CreateClientCommand>(job.PayloadJson, _json)!, ct),
                ProvisioningJobTypes.RenewClient => await _xui.RenewClientAsync(JsonSerializer.Deserialize<RenewClientCommand>(job.PayloadJson, _json)!, ct),
                ProvisioningJobTypes.DisableClient => await _xui.DisableClientAsync(JsonSerializer.Deserialize<DisableClientCommand>(job.PayloadJson, _json)!, ct),
                _ => throw new NotSupportedException($"Unsupported job type: {job.Type}")
            };
            await SendResultAsync(job.JobId, "Succeeded", JsonSerializer.Serialize(result, _json), null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed", job.JobId);
            await SendResultAsync(job.JobId, "Failed", null, ex.Message, ct);
        }
    }

    private async Task SendResultAsync(Guid jobId, string status, string? resultJson, string? error, CancellationToken ct)
    {
        var req = new AgentJobResultRequest(_options.AgentId, jobId, status, resultJson, error);
        using var resp = await _client.PostSignedAsync($"api/agent/jobs/{jobId}/result", req, ct);
        if (!resp.IsSuccessStatusCode) _logger.LogWarning("Send result failed: {Status}", resp.StatusCode);
    }
}
