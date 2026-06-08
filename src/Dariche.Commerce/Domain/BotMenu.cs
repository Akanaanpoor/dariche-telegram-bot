namespace Dariche.Commerce.Domain;

public sealed class BotMenu
{
    public Guid Id { get; set; }

    public string Key { get; set; } = default!;

    public string Text { get; set; } = default!;

    public int Order { get; set; }

    public bool IsActive { get; set; }
}