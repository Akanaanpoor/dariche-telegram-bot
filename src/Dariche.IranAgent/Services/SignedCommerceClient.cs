using System.Net.Http.Json;
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
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public SignedCommerceClient(HttpClient http, IOptions<AgentOptions> options)
    {
        _http = http;
        _options = options.Value;
        _http.BaseAddress = new Uri(_options.CommerceBaseUrl.TrimEnd('/') + "/");
    }

    public Task<HttpResponseMessage> PostSignedAsync<T>(string path, T payload, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(payload, _json);
        var req = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        Sign(req, path, body);
        return _http.SendAsync(req, ct);
    }

    public Task<HttpResponseMessage> GetSignedAsync(string path, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        Sign(req, path, string.Empty);
        return _http.SendAsync(req, ct);
    }

    private void Sign(HttpRequestMessage req, string path, string body)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = Guid.NewGuid().ToString("N");
        var signature = HmacSigner.ComputeSignature(_options.AgentSecret, req.Method.Method, path, timestamp, nonce, body);
        req.Headers.Add("X-Dariche-Agent-Id", _options.AgentId);
        req.Headers.Add("X-Dariche-Timestamp", timestamp);
        req.Headers.Add("X-Dariche-Nonce", nonce);
        req.Headers.Add("X-Dariche-Signature", signature);
    }
}
