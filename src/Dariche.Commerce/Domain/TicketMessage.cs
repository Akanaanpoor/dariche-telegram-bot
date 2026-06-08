namespace Dariche.Commerce.Domain;

public sealed class TicketMessage
{
    public Guid Id { get; set; }

    public Guid TicketId { get; set; }

    public bool IsAdmin { get; set; }

    public string Message { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}