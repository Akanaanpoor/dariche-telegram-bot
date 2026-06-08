namespace Dariche.Commerce.Domain;

public sealed class InboundGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public List<InboundGroupItem> Items { get; set; } = new();
}