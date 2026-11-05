using System.ComponentModel.DataAnnotations;

namespace StockDesk.Api.Domain;

/// <summary>
/// Representa el nivel de existencias de un producto en un almacén específico,
/// junto con su costo promedio ponderado vigente.
/// </summary>
public class Stock
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }

    /// <summary>
    /// Token de concurrencia optimista. SQL Server lo mapea a rowversion;
    /// en SQLite (pruebas) EF Core lo simula como columna de concurrencia.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
