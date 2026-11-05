using System.ComponentModel.DataAnnotations;
using StockDesk.Api.Domain;

namespace StockDesk.Api.Dtos;

public class StockInRequest
{
    public StockInRequest()
    {
    }

    public StockInRequest(int productId, int warehouseId, decimal quantity, decimal unitCost, string? note)
    {
        ProductId = productId;
        WarehouseId = warehouseId;
        Quantity = quantity;
        UnitCost = unitCost;
        Note = note;
    }

    [Required]
    public int ProductId { get; set; }

    [Required]
    public int WarehouseId { get; set; }

    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitCost { get; set; }

    public string? Note { get; set; }
}

public class StockOutRequest
{
    public StockOutRequest()
    {
    }

    public StockOutRequest(int productId, int warehouseId, decimal quantity, string? note)
    {
        ProductId = productId;
        WarehouseId = warehouseId;
        Quantity = quantity;
        Note = note;
    }

    [Required]
    public int ProductId { get; set; }

    [Required]
    public int WarehouseId { get; set; }

    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; }

    public string? Note { get; set; }
}

public class StockTransferRequest
{
    public StockTransferRequest()
    {
    }

    public StockTransferRequest(int productId, int sourceWarehouseId, int destinationWarehouseId, decimal quantity, string? note)
    {
        ProductId = productId;
        SourceWarehouseId = sourceWarehouseId;
        DestinationWarehouseId = destinationWarehouseId;
        Quantity = quantity;
        Note = note;
    }

    [Required]
    public int ProductId { get; set; }

    [Required]
    public int SourceWarehouseId { get; set; }

    [Required]
    public int DestinationWarehouseId { get; set; }

    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; }

    public string? Note { get; set; }
}

public record MovementDto(
    int Id,
    int ProductId,
    string ProductSku,
    string ProductName,
    int WarehouseId,
    string WarehouseName,
    int? DestinationWarehouseId,
    string? DestinationWarehouseName,
    MovementType Type,
    decimal Quantity,
    decimal? UnitCost,
    decimal ResultingAverageCost,
    string? Note,
    DateTime CreatedAtUtc,
    int CreatedByUserId);
