namespace Dariche.Commerce.Domain;

public sealed class Plan
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int TrafficGb { get; set; }
    public int DurationDays { get; set; }
    public decimal PriceToman { get; set; }
    public int? PriceStars { get; set; }
    public string InboundGroupCode { get; set; } = "default";
    public bool AutoRenew { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    
    // Navigation properties
    public List<Order> Orders { get; set; } = new();
}