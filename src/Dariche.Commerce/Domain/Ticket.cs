namespace Dariche.Commerce.Domain;

public class Ticket
{
    public Guid Id { get; set; }

    public Guid TelegramUserId { get; set; }

    public TicketStatus Status { get; set; }

    public string Subject { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? ClosedAtUtc { get; set; }
}