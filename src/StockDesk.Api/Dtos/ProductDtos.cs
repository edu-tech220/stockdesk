using System.ComponentModel.DataAnnotations;

namespace StockDesk.Api.Dtos;

public record ProductDto(
    int Id,
    string Sku,
    string Name,
    string? Description,
    string Unit,
    int ReorderPoint,
    bool IsActive);

public class CreateProductRequest
{
    public CreateProductRequest()
    {
    }

    public CreateProductRequest(string sku, string name, string? description, string unit, int reorderPoint)
    {
        Sku = sku;
        Name = name;
        Description = description;
        Unit = unit;
        ReorderPoint = reorderPoint;
    }

    [Required]
    public string Sku { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public string Unit { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int ReorderPoint { get; set; }
}
