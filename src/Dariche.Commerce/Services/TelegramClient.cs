using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Dariche.Commerce.Options;

namespace Dariche.Commerce.Services;

public sealed class TelegramClient
{
    private readonly HttpClient _http;
    private readonly BotOptions _options;
    private readonly ILogger<TelegramClient> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public TelegramClient(HttpClient http, IOptions<BotOptions> options, ILogger<TelegramClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    private string Api(string method) => $"https://api.telegram.org/bot{_options.Token}/{method}";

    public async Task<JsonDocument> GetUpdatesAsync(long offset, CancellationToken ct)
    {
        var url = Api($"getUpdates?timeout=25&offset={offset}&allowed_updates=['message','callback_query']");
        using var resp = await _http.GetAsync(url, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        resp.EnsureSuccessStatusCode();
        return JsonDocument.Parse(text);
    }

    public async Task SendTextAsync(long chatId, string text, CancellationToken ct = default, object? replyMarkup = null)
    {
        const int chunk = 3800;
        if (text.Length <= chunk)
        {
            await SendRawAsync(chatId, text, replyMarkup, ct);
            return;
        }
        
        var total = (int)Math.Ceiling(text.Length / (double)chunk);
        for (var i = 0; i < total; i++)
        {
            var part = text.Substring(i * chunk, Math.Min(chunk, text.Length - i * chunk));
            await SendRawAsync(chatId, $"📄 قسمت {i + 1}/{total}\n\n{part}", replyMarkup, ct);
        }
    }

    private async Task SendRawAsync(long chatId, string text, object? replyMarkup, CancellationToken ct)
    {
        var payload = new Dictionary<string, object?>
        {
            ["chat_id"] = chatId,
            ["text"] = text,
            ["parse_mode"] = "Markdown",
            ["disable_web_page_preview"] = true
        };
        
        if (replyMarkup != null)
        {
            payload["reply_markup"] = replyMarkup;
        }
        
        var json = JsonSerializer.Serialize(payload, _json);
        using var resp = await _http.PostAsync(Api("sendMessage"), new StringContent(json, Encoding.UTF8, "application/json"), ct);
        
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Telegram sendMessage failed: {Status} {Body}", resp.StatusCode, body);
        }
    }

    public async Task AnswerCallbackQueryAsync(string callbackQueryId, CancellationToken ct)
    {
        var payload = new { callback_query_id = callbackQueryId };
        var json = JsonSerializer.Serialize(payload, _json);
        await _http.PostAsync(Api("answerCallbackQuery"), new StringContent(json, Encoding.UTF8, "application/json"), ct);
    }
}