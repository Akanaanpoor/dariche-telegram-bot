using System.Security.Cryptography;
using System.Text.Json;
using Dariche.IranAgent.Options;
using Dariche.Shared.Provisioning;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Dariche.IranAgent.Xui;

public sealed class XuiSqliteProvisioner
{
    private readonly XuiOptions _options;
    private readonly ILogger<XuiSqliteProvisioner> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public XuiSqliteProvisioner(IOptions<XuiOptions> options, ILogger<XuiSqliteProvisioner> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CreateClientResult> CreateClientAsync(CreateClientCommand cmd, CancellationToken ct)
    {
        BackupIfNeeded();
        var uuid = Guid.NewGuid().ToString();
        var subId = RandomAlnum(18);
        var expireAt = DateTimeOffset.UtcNow.AddDays(cmd.DurationDays);
        var expiryMs = expireAt.ToUnixTimeMilliseconds();
        var totalBytes = checked(cmd.TrafficGb * 1024L * 1024L * 1024L);
        var assigned = new List<string>();

        await using var con = new SqliteConnection($"Data Source={_options.DbPath}");
        await con.OpenAsync(ct);

        await using var tx =
            (SqliteTransaction)await con.BeginTransactionAsync(ct);

        var inbounds = await FindInboundsAsync(con, tx, cmd.AssignedInboundTags, ct);
        if (inbounds.Count == 0) throw new InvalidOperationException("No matching inbounds found for requested tags.");

        long? globalClientId = null;
        if (await TableExistsAsync(con, tx, "clients", ct))
        {
            globalClientId = await UpsertGlobalClientAsync(con, tx, cmd.ClientEmail, uuid, subId, totalBytes, expiryMs, cmd.ClientLabel, ct);
        }

        foreach (var inbound in inbounds)
        {
            var settings = JsonSerializer.Deserialize<JsonElement>(inbound.SettingsJson);
            var updatedSettings = AddOrReplaceClient(settings, cmd.ClientEmail, uuid, subId, totalBytes, expiryMs, cmd.ClientLabel);
            await ExecAsync(con, tx, "update inbounds set settings=$settings where id=$id", ct,
                ("$settings", updatedSettings), ("$id", inbound.Id));

            if (globalClientId.HasValue && await TableExistsAsync(con, tx, "client_inbounds", ct))
            {
                await ExecAsync(con, tx, "delete from client_inbounds where client_id=$client_id and inbound_id=$inbound_id", ct,
                    ("$client_id", globalClientId.Value), ("$inbound_id", inbound.Id));
                await InsertClientInboundAsync(con, tx, globalClientId.Value, inbound.Id, ct);
            }

            if (await TableExistsAsync(con, tx, "client_traffics", ct))
            {
                await ExecAsync(con, tx, "delete from client_traffics where inbound_id=$inbound_id and email=$email", ct,
                    ("$inbound_id", inbound.Id), ("$email", cmd.ClientEmail));
                await InsertClientTrafficAsync(con, tx, inbound.Id, cmd.ClientEmail, totalBytes, expiryMs, ct);
            }

            assigned.Add(inbound.Tag ?? inbound.Remark ?? inbound.Port.ToString());
        }

        await tx.CommitAsync(ct);
        return new CreateClientResult(uuid, cmd.ClientEmail, subId, BuildSubscriptionUrl(subId), assigned.ToArray(), expireAt, cmd.TrafficGb);
    }

    public async Task<RenewClientResult> RenewClientAsync(RenewClientCommand cmd, CancellationToken ct)
    {
        BackupIfNeeded();
        var newExpire = DateTimeOffset.UtcNow.AddDays(cmd.AdditionalDays);
        var expiryMs = newExpire.ToUnixTimeMilliseconds();
        await using var con = new SqliteConnection($"Data Source={_options.DbPath}");
        await con.OpenAsync(ct);
        await using var tx =
            (SqliteTransaction)await con.BeginTransactionAsync(ct);
        var inbounds = await FindInboundsAsync(con, tx, cmd.AssignedInboundTags, ct);
        foreach (var inbound in inbounds)
        {
            var settings = JsonSerializer.Deserialize<JsonElement>(inbound.SettingsJson);
            var updated = UpdateClient(settings, cmd.ClientEmail, enable: null, expiryMs: expiryMs);
            await ExecAsync(con, tx, "update inbounds set settings=$settings where id=$id", ct, ("$settings", updated), ("$id", inbound.Id));
            if (await TableExistsAsync(con, tx, "client_traffics", ct))
                await ExecAsync(con, tx, "update client_traffics set expiry_time=$expiry where inbound_id=$inbound and email=$email", ct, ("$expiry", expiryMs), ("$inbound", inbound.Id),
                    ("$email", cmd.ClientEmail));
        }

        if (await TableExistsAsync(con, tx, "clients", ct))
            await ExecAsync(con, tx, "update clients set expiry_time=$expiry, enable=1 where email=$email", ct, ("$expiry", expiryMs), ("$email", cmd.ClientEmail));
        await tx.CommitAsync(ct);
        return new RenewClientResult(cmd.ClientEmail, newExpire);
    }

    public async Task<DisableClientResult> DisableClientAsync(DisableClientCommand cmd, CancellationToken ct)
    {
        BackupIfNeeded();
        await using var con = new SqliteConnection($"Data Source={_options.DbPath}");
        await con.OpenAsync(ct);
        await using var tx =
            (SqliteTransaction)await con.BeginTransactionAsync(ct);
        var inbounds = await FindInboundsAsync(con, tx, cmd.AssignedInboundTags, ct);
        foreach (var inbound in inbounds)
        {
            var settings = JsonSerializer.Deserialize<JsonElement>(inbound.SettingsJson);
            var updated = UpdateClient(settings, cmd.ClientEmail, enable: false, expiryMs: null);
            await ExecAsync(con, tx, "update inbounds set settings=$settings where id=$id", ct, ("$settings", updated), ("$id", inbound.Id));
            if (await TableExistsAsync(con, tx, "client_traffics", ct))
                await ExecAsync(con, tx, "update client_traffics set enable=0 where inbound_id=$inbound and email=$email", ct, ("$inbound", inbound.Id), ("$email", cmd.ClientEmail));
        }

        if (await TableExistsAsync(con, tx, "clients", ct))
            await ExecAsync(con, tx, "update clients set enable=0 where email=$email", ct, ("$email", cmd.ClientEmail));
        await tx.CommitAsync(ct);
        return new DisableClientResult(cmd.ClientEmail, true);
    }

    private string BuildSubscriptionUrl(string subId) => _options.SubscriptionBaseUrl.TrimEnd('/') + "/" + subId;

    private void BackupIfNeeded()
    {
        if (!_options.CreateBackupBeforeWrite) return;
        Directory.CreateDirectory(_options.BackupDirectory);
        var dest = Path.Combine(_options.BackupDirectory, $"x-ui-before-agent-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.db");
        File.Copy(_options.DbPath, dest, overwrite: false);
    }

    private static string RandomAlnum(int len)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        Span<byte> bytes = stackalloc byte[len];
        RandomNumberGenerator.Fill(bytes);
        return new string(bytes.ToArray().Select(b => chars[b % chars.Length]).ToArray());
    }

    private sealed record InboundRow(long Id, string? Remark, string? Tag, int Port, string SettingsJson);

    private static async Task<List<InboundRow>> FindInboundsAsync(SqliteConnection con, SqliteTransaction tx, string[] tags, CancellationToken ct)
    {
        var result = new List<InboundRow>();
        foreach (var tag in tags.Distinct())
        {
            await using var cmd = con.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "select id, remark, tag, port, settings from inbounds where tag=$tag or remark=$tag limit 1";
            cmd.Parameters.AddWithValue("$tag", tag);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct)) result.Add(new InboundRow(r.GetInt64(0), r.IsDBNull(1) ? null : r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetInt32(3), r.GetString(4)));
        }

        return result;
    }

    private static string AddOrReplaceClient(JsonElement settings, string email, string uuid, string subId, long totalBytes, long expiryMs, string label)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(settings.GetRawText()) ?? new();
        var clients = new List<Dictionary<string, object?>>();
        if (dict.TryGetValue("clients", out var raw) && raw is JsonElement el && el.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in el.EnumerateArray())
            {
                var cd = JsonSerializer.Deserialize<Dictionary<string, object?>>(c.GetRawText()) ?? new();
                if (!string.Equals(cd.GetValueOrDefault("email")?.ToString(), email, StringComparison.OrdinalIgnoreCase)) clients.Add(cd);
            }
        }

        clients.Add(new Dictionary<string, object?>
        {
            ["id"] = uuid, ["flow"] = "", ["email"] = email, ["limitIp"] = 0,
            ["totalGB"] = totalBytes, ["expiryTime"] = expiryMs, ["enable"] = true,
            ["tgId"] = "", ["subId"] = subId, ["comment"] = label, ["reset"] = 0
        });
        dict["clients"] = clients;
        dict.TryAdd("decryption", "none");
        return JsonSerializer.Serialize(dict);
    }

    private static string UpdateClient(JsonElement settings, string email, bool? enable, long? expiryMs)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(settings.GetRawText()) ?? new();
        if (dict.TryGetValue("clients", out var raw) && raw is JsonElement el && el.ValueKind == JsonValueKind.Array)
        {
            var clients = new List<Dictionary<string, object?>>();
            foreach (var c in el.EnumerateArray())
            {
                var cd = JsonSerializer.Deserialize<Dictionary<string, object?>>(c.GetRawText()) ?? new();
                if (string.Equals(cd.GetValueOrDefault("email")?.ToString(), email, StringComparison.OrdinalIgnoreCase))
                {
                    if (enable.HasValue) cd["enable"] = enable.Value;
                    if (expiryMs.HasValue) cd["expiryTime"] = expiryMs.Value;
                }

                clients.Add(cd);
            }

            dict["clients"] = clients;
        }

        return JsonSerializer.Serialize(dict);
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection con, SqliteTransaction tx, string table, CancellationToken ct)
    {
        await using var cmd = con.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "select count(*) from sqlite_master where type='table' and name=$name";
        cmd.Parameters.AddWithValue("$name", table);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
    }

    private static async Task<HashSet<string>> ColumnsAsync(
        SqliteConnection con,
        SqliteTransaction tx,
        string table,
        CancellationToken ct)
    {
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var cmd = con.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"pragma table_info({table})";

        await using var r = await cmd.ExecuteReaderAsync(ct);

        while (await r.ReadAsync(ct))
            cols.Add(r.GetString(1));

        return cols;
    }

    private static async Task<long> UpsertGlobalClientAsync(SqliteConnection con, SqliteTransaction tx, string email, string uuid, string subId, long totalBytes, long expiryMs, string label,
        CancellationToken ct)
    {
        await ExecAsync(con, tx, "delete from clients where email=$email", ct, ("$email", email));
        var cols = await ColumnsAsync(con, tx, "clients", ct);
        var values = new Dictionary<string, object?>
        {
            ["email"] = email, ["sub_id"] = subId, ["uuid"] = uuid, ["password"] = "", ["auth"] = "",
            ["flow"] = "", ["security"] = "", ["reverse"] = "", ["limit_ip"] = 0, ["total_gb"] = totalBytes,
            ["expiry_time"] = expiryMs, ["enable"] = 1, ["tg_id"] = null, ["group_name"] = "", ["comment"] = label,
            ["reset"] = 0, ["created_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), ["updated_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        var use = values.Where(kv => cols.Contains(kv.Key)).ToList();
        var names = string.Join(",", use.Select(x => x.Key));
        var ps = string.Join(",", use.Select(x => "$" + x.Key));
        await using var cmd = con.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"insert into clients ({names}) values ({ps}); select last_insert_rowid();";
        foreach (var kv in use) cmd.Parameters.AddWithValue("$" + kv.Key, kv.Value ?? DBNull.Value);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
    }

    private static async Task InsertClientInboundAsync(SqliteConnection con, SqliteTransaction tx, long clientId, long inboundId, CancellationToken ct)
    {
        var cols = await ColumnsAsync(con, tx, "client_inbounds", ct);
        var values = new Dictionary<string, object?>
            { ["client_id"] = clientId, ["inbound_id"] = inboundId, ["flow_override"] = "", ["created_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        var use = values.Where(kv => cols.Contains(kv.Key)).ToList();
        await using var cmd = con.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"insert into client_inbounds ({string.Join(",", use.Select(x => x.Key))}) values ({string.Join(",", use.Select(x => "$" + x.Key))})";
        foreach (var kv in use) cmd.Parameters.AddWithValue("$" + kv.Key, kv.Value ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertClientTrafficAsync(SqliteConnection con, SqliteTransaction tx, long inboundId, string email, long totalBytes, long expiryMs, CancellationToken ct)
    {
        var cols = await ColumnsAsync(con, tx, "client_traffics", ct);
        var values = new Dictionary<string, object?>
        {
            ["inbound_id"] = inboundId, ["enable"] = 1, ["email"] = email, ["up"] = 0, ["down"] = 0, ["expiry_time"] = expiryMs, ["total"] = totalBytes, ["reset"] = 0, ["last_online"] = 0
        };
        var use = values.Where(kv => cols.Contains(kv.Key)).ToList();
        await using var cmd = con.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"insert into client_traffics ({string.Join(",", use.Select(x => x.Key))}) values ({string.Join(",", use.Select(x => "$" + x.Key))})";
        foreach (var kv in use) cmd.Parameters.AddWithValue("$" + kv.Key, kv.Value ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task ExecAsync(SqliteConnection con, SqliteTransaction tx, string sql, CancellationToken ct, params (string Name, object? Value)[] ps)
    {
        await using var cmd = con.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var p in ps) cmd.Parameters.AddWithValue(p.Name, p.Value ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}