namespace Dariche.Commerce.Domain;

public sealed class TelegramUser
{
    public Guid Id { get; set; }

    public long TelegramId { get; set; }

    public string? Username { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public UserStatus Status { get; set; }

    public bool IsAdmin { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset LastSeenAtUtc { get; set; }
}