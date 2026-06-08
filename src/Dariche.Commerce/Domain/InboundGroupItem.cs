namespace Dariche.Commerce.Domain;

public sealed class InboundGroupItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InboundGroupId { get; set; }
    public InboundGroup? InboundGroup { get; set; }
    public string InboundTag { get; set; } = default!;
    public int SortOrder { get; set; }
}