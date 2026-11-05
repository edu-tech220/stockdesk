namespace StockDesk.Web.Models;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string Username, string Role, DateTime ExpiresAtUtc);

public record WarehouseDto(int Id, string Code, string Name, string Location, bool IsActive);
public record CreateWarehouseRequest(string Code, string Name, string? Location);

public record ProductDto(int Id, string Sku, string Name, string? Description, string Unit, int ReorderPoint, bool IsActive);
public record CreateProductRequest(string Sku, string Name, string? Description, string Unit, int ReorderPoint);

public record StockDto(
    int ProductId,
    string ProductSku,
    string ProductName,
    int WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    decimal Quantity,
    decimal AverageCost);

public record LowStockItemDto(
    int ProductId,
    string ProductSku,
    string ProductName,
    int WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    decimal Quantity,
    int ReorderPoint);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public enum MovementType
{
    In = 0,
    Out = 1,
    Transfer = 2,
    Adjustment = 3
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

public record StockInRequest(int ProductId, int WarehouseId, decimal Quantity, decimal UnitCost, string? Note);
public record StockOutRequest(int ProductId, int WarehouseId, decimal Quantity, string? Note);
public record StockTransferRequest(int ProductId, int SourceWarehouseId, int DestinationWarehouseId, decimal Quantity, string? Note);

public class ApiProblemDetails
{
    public string? Title { get; set; }
    public string? Detail { get; set; }
    public int? Status { get; set; }
}
