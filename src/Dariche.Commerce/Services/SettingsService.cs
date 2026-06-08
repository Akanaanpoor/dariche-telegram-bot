using Dariche.Commerce.Data;
using Dariche.Commerce.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Dariche.Commerce.Services;

public sealed class SettingsService
    : ISettingsService
{
    private readonly CommerceDbContext _db;

    private readonly IMemoryCache _cache;

    public SettingsService(
        CommerceDbContext db,
        IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<string?> GetAsync(
        string key,
        CancellationToken ct = default)
    {
        return await _cache.GetOrCreateAsync(
            $"settings:{key}",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromMinutes(30);

                return await _db.Settings
                    .Where(x => x.Key == key)
                    .Select(x => x.Value)
                    .FirstOrDefaultAsync(ct);
            });
    }

    public async Task SetAsync(
        string key,
        string value,
        CancellationToken ct = default)
    {
        var setting =
            await _db.Settings
                .FirstOrDefaultAsync(
                    x => x.Key == key,
                    ct);

        if (setting == null)
        {
            setting = new Settings
            {
                Key = key,
                Value = value
            };

            _db.Settings.Add(setting);
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        _cache.Remove($"settings:{key}");
    }
}