namespace StockDesk.Api.Dtos;

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
