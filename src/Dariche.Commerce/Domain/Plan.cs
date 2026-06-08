namespace Dariche.Commerce.Domain;

public sealed class Plan
{
    public Guid Id { get; set; }

    public string Code { get; set; }

    public string Name { get; set; }

    public int TrafficGb { get; set; }

    public int DurationDays { get; set; }

    public decimal PriceToman { get; set; }

    public bool AutoRenew { get; set; }

    public bool IsActive { get; set; }

    public string? Description { get; set; }
}