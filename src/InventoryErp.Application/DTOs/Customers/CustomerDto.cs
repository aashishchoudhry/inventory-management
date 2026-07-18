namespace InventoryErp.Application.DTOs.Customers;

public sealed record CustomerDto
{
    public Guid Id { get; init; }
    public Guid CompanyId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? Mobile { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? Address { get; init; }
}
