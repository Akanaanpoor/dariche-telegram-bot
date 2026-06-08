using System.Data.SQLite;
using System.Text;
using System.Text.Json;
using Dariche.Shared.Provisioning;

namespace Dariche.IranAgent.Xui;

public sealed class XuiSqliteProvisioner
{
    private readonly string _dbPath;
    private readonly string _subscriptionBaseUrl;
    private readonly ILogger<XuiSqliteProvisioner> _logger;

    public XuiSqliteProvisioner(IConfiguration config, ILogger<XuiSqliteProvisioner> logger)
    {
        _dbPath = config["Xui:DbPath"] ?? "/etc/x-ui/x-ui.db";
        _subscriptionBaseUrl = config["Xui:SubscriptionBaseUrl"] ?? "https://example.com/sub";
        _logger = logger;
    }

    public async Task<CreateClientResult> CreateClientAsync(CreateClientCommand cmd, CancellationToken ct)
    {
        // Create backup first
        await BackupDatabaseAsync(ct);
        
        var connectionString = $"Data Source={_dbPath};Version=3;";
        
        using var connection = new SQLiteConnection(connectionString);
        await connection.OpenAsync(ct);
        
        // Generate UUID v4
        var clientUuid = Guid.NewGuid().ToString();
        var subId = cmd.SubId;
        var expiry = DateTimeOffset.UtcNow.AddDays(cmd.DurationDays);
        
        // Get current max client id
        var maxIdCmd = new SQLiteCommand("SELECT MAX(id) FROM clients", connection);
        var nextId = (Convert.ToInt32(await maxIdCmd.ExecuteScalarAsync(ct)) + 1);
        
        // Create client JSON for x-ui
        var clientJson = $@"{{
            ""id"": {nextId},
            ""email"": ""{cmd.ClientEmail}"",
            ""uuid"": ""{clientUuid}"",
            ""enable"": true,
            ""expiryTime"": {expiry.ToUnixTimeSeconds()},
            ""totalGB"": {cmd.TrafficGb},
            ""limitIp"": 0,
            ""flow"": """"
        }}";
        
        // Insert into inbounds table for each tag
        var success = true;
        var assignedTags = new List<string>();
        
        foreach (var tag in cmd.InboundTags)
        {
            try
            {
                // Get current inbound settings
                var selectCmd = new SQLiteCommand("SELECT settings FROM inbounds WHERE tag = @tag", connection);
                selectCmd.Parameters.AddWithValue("@tag", tag);
                var settingsJson = await selectCmd.ExecuteScalarAsync(ct) as string;
                
                if (string.IsNullOrEmpty(settingsJson))
                {
                    _logger.LogWarning("Inbound tag {Tag} not found", tag);
                    continue;
                }
                
                // Parse settings and add client
                var settings = JsonSerializer.Deserialize<InboundSettings>(settingsJson);
                if (settings?.Clients == null)
                    settings = new InboundSettings { Clients = new List<Client>() };
                
                settings.Clients.Add(new Client
                {
                    Id = nextId,
                    Email = cmd.ClientEmail,
                    Uuid = clientUuid,
                    Enable = true,
                    ExpiryTime = expiry.ToUnixTimeSeconds(),
                    TotalGB = cmd.TrafficGb,
                    LimitIp = 0
                });
                
                var newSettingsJson = JsonSerializer.Serialize(settings);
                var updateCmd = new SQLiteCommand("UPDATE inbounds SET settings = @settings WHERE tag = @tag", connection);
                updateCmd.Parameters.AddWithValue("@settings", newSettingsJson);
                updateCmd.Parameters.AddWithValue("@tag", tag);
                await updateCmd.ExecuteNonQueryAsync(ct);
                
                assignedTags.Add(tag);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add client to inbound {Tag}", tag);
                success = false;
            }
        }
        
        if (!success && assignedTags.Count == 0)
            throw new Exception("Failed to add client to any inbound");
        
        // Generate subscription URL
        var subscriptionUrl = $"{_subscriptionBaseUrl}/{subId}";
        
        // Also add to subscription_links table if exists
        try
        {
            var insertSubCmd = new SQLiteCommand(
                "INSERT OR REPLACE INTO subscription_links (sub_id, email, uuid, expiry, traffic_gb) VALUES (@sub_id, @email, @uuid, @expiry, @traffic)",
                connection);
            insertSubCmd.Parameters.AddWithValue("@sub_id", subId);
            insertSubCmd.Parameters.AddWithValue("@email", cmd.ClientEmail);
            insertSubCmd.Parameters.AddWithValue("@uuid", clientUuid);
            insertSubCmd.Parameters.AddWithValue("@expiry", expiry.ToUnixTimeSeconds());
            insertSubCmd.Parameters.AddWithValue("@traffic", cmd.TrafficGb);
            await insertSubCmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not insert into subscription_links table (may not exist)");
        }
        
        return new CreateClientResult(
            ClientEmail: cmd.ClientEmail,
            ClientUuid: clientUuid,
            SubId: subId,
            SubscriptionUrl: subscriptionUrl,
            ExpireAtUtc: expiry,
            TrafficGb: cmd.TrafficGb,
            AssignedInboundTags: assignedTags.ToArray(),
            Extra: new[] { ("OrderId", (object?)cmd.OrderId), ("TelegramUserId", cmd.TelegramUserId) }
        );
    }
    
    private async Task BackupDatabaseAsync(CancellationToken ct)
    {
        var backupPath = $"{_dbPath}.backup.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        await Task.Run(() => File.Copy(_dbPath, backupPath, true), ct);
        _logger.LogInformation("Database backed up to {BackupPath}", backupPath);
        
        // Keep only last 10 backups
        var backupDir = Path.GetDirectoryName(_dbPath)!;
        var backups = Directory.GetFiles(backupDir, "*.db.backup.*")
            .OrderByDescending(f => f)
            .Skip(10);
        foreach (var oldBackup in backups)
        {
            try { File.Delete(oldBackup); } catch { }
        }
    }
    
    // Helper classes for JSON serialization
    private class InboundSettings
    {
        public List<Client> Clients { get; set; } = new();
        public string? Protocol { get; set; }
        public object? Settings { get; set; }
        public object? StreamSettings { get; set; }
    }
    
    private class Client
    {
        public int Id { get; set; }
        public string Email { get; set; } = "";
        public string Uuid { get; set; } = "";
        public bool Enable { get; set; }
        public long ExpiryTime { get; set; }
        public long TotalGB { get; set; }
        public int LimitIp { get; set; }
    }
}