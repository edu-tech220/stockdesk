namespace StockDesk.Api.Domain;

public enum MovementType
{
    In = 0,
    Out = 1,
    Transfer = 2,
    Adjustment = 3
}

/// <summary>
/// Registro histórico (kardex) de un movimiento de inventario.
/// </summary>
public class StockMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Almacén origen del movimiento (o único almacén si no es transferencia).</summary>
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    /// <summary>Solo aplica cuando Type == Transfer.</summary>
    public int? DestinationWarehouseId { get; set; }
    public Warehouse? DestinationWarehouse { get; set; }

    public MovementType Type { get; set; }
    public decimal Quantity { get; set; }

    /// <summary>Costo unitario de la entrada. Requerido solo para movimientos In.</summary>
    public decimal? UnitCost { get; set; }

    /// <summary>Costo promedio ponderado resultante tras aplicar el movimiento.</summary>
    public decimal ResultingAverageCost { get; set; }

    public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
}
