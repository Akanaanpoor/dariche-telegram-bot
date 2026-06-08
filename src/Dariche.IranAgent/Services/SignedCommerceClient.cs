using System.Text;
using System.Text.Json;
using Dariche.IranAgent.Options;
using Dariche.Shared.Agent;
using Microsoft.Extensions.Options;

namespace Dariche.IranAgent.Services;

public sealed class SignedCommerceClient
{
    private readonly HttpClient _http;
    private readonly AgentOptions _options;
    private readonly JsonSerializerOptions _json;
    private readonly ILogger<SignedCommerceClient> _logger;

    public SignedCommerceClient(
        HttpClient http,
        IOptions<AgentOptions> options,
        ILogger<SignedCommerceClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        _json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    public async Task<HttpResponseMessage> GetSignedAsync(string path, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        await SignRequestAsync(request, null, ct);
        return await _http.SendAsync(request, ct);
    }

    public async Task<HttpResponseMessage> PostSignedAsync<T>(string path, T data, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        var json = JsonSerializer.Serialize(data, _json);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        await SignRequestAsync(request, json, ct);
        return await _http.SendAsync(request, ct);
    }

    private async Task SignRequestAsync(HttpRequestMessage request, string? body, CancellationToken ct)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = Guid.NewGuid().ToString("N");
        var path = request.RequestUri?.PathAndQuery ?? "";
        
        var signature = HmacSigner.ComputeSignature(
            _options.AgentSecret,
            request.Method.Method,
            path,
            timestamp,
            nonce,
            body ?? ""
        );
        
        request.Headers.Add("X-Dariche-Agent-Id", _options.AgentId);
        request.Headers.Add("X-Dariche-Timestamp", timestamp);
        request.Headers.Add("X-Dariche-Nonce", nonce);
        request.Headers.Add("X-Dariche-Signature", signature);
        
        _logger.LogDebug("Signed request to {Path}", path);
    }
}