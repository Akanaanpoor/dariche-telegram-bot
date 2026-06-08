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
    private readonly JsonSerializerOptions _json;

    public AgentWorker(
        SignedCommerceClient client, 
        XuiSqliteProvisioner xui, 
        IOptions<AgentOptions> options, 
        ILogger<AgentWorker> logger)
    {
        _client = client;
        _xui = xui;
        _options = options.Value;
        _logger = logger;
        _json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Agent worker started for agent {AgentId}", _options.AgentId);
        
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
        try
        {
            var req = new AgentHeartbeatRequest(
                Hostname: Environment.MachineName,
                Version: "0.1.0"
            );
            
            using var resp = await _client.PostSignedAsync("api/agent/heartbeat", req, ct);
            
            if (!resp.IsSuccessStatusCode)
                _logger.LogWarning("Heartbeat failed: {Status}", resp.StatusCode);
            else
                _logger.LogDebug("Heartbeat sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Heartbeat request failed");
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        using var resp = await _client.GetSignedAsync("api/agent/jobs/next", ct);
        
        if (resp.StatusCode == HttpStatusCode.NoContent)
        {
            _logger.LogDebug("No pending jobs");
            return;
        }
        
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Get job failed: {Status}", resp.StatusCode);
            return;
        }
        
        var job = await resp.Content.ReadFromJsonAsync<AgentJobEnvelope>(_json, ct);
        if (job is null)
        {
            _logger.LogWarning("Received empty job response");
            return;
        }
        
        _logger.LogInformation("Picked job {JobId} type {Type}", job.JobId, job.Type);
        await ExecuteJobAsync(job, ct);
    }

    private async Task ExecuteJobAsync(AgentJobEnvelope job, CancellationToken ct)
    {
        try
        {
            object? result = null;
            
            // تبدیل string type به enum
            if (job.Type == "CreateClient" || job.Type == "0")
            {
                var cmd = JsonSerializer.Deserialize<CreateClientCommand>(job.PayloadJson, _json);
                if (cmd is null)
                    throw new InvalidOperationException("Failed to deserialize CreateClientCommand");
                    
                result = await _xui.CreateClientAsync(cmd, ct);
            }
            else if (job.Type == "RenewClient" || job.Type == "1")
            {
                throw new NotSupportedException("RenewClient not implemented yet");
            }
            else if (job.Type == "DisableClient" || job.Type == "2")
            {
                throw new NotSupportedException("DisableClient not implemented yet");
            }
            else
            {
                throw new NotSupportedException($"Unsupported job type: {job.Type}");
            }
            
            await SendResultAsync(job.JobId, "Succeeded", JsonSerializer.Serialize(result, _json), null, ct);
            _logger.LogInformation("Job {JobId} completed successfully", job.JobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed", job.JobId);
            await SendResultAsync(job.JobId, "Failed", null, ex.Message, ct);
        }
    }

    private async Task SendResultAsync(Guid jobId, string status, string? resultJson, string? error, CancellationToken ct)
    {
        try
        {
            var req = new AgentJobResultRequest(
                Status: status,
                ResultJson: resultJson,
                ErrorMessage: error
            );
            
            using var resp = await _client.PostSignedAsync($"api/agent/jobs/{jobId}/result", req, ct);
            
            if (!resp.IsSuccessStatusCode)
                _logger.LogWarning("Send result failed: {Status}", resp.StatusCode);
            else
                _logger.LogDebug("Result for job {JobId} sent successfully", jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send result for job {JobId}", jobId);
        }
    }
}