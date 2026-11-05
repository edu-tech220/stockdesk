namespace StockDesk.Api.Domain;

public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Unit { get; set; } = "pieza";
    public int ReorderPoint { get; set; }
    public bool IsActive { get; set; } = true;
}
