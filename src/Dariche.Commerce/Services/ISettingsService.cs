namespace Dariche.Commerce.Services;

public interface ISettingsService
{
    Task<string?> GetAsync(
        string key,
        CancellationToken ct = default);

    Task SetAsync(
        string key,
        string value,
        CancellationToken ct = default);
}