using Dariche.Commerce.Domain;

public sealed class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long TelegramUserId { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public string Subject { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAtUtc { get; set; }
}