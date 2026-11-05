using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockDesk.Api.Data;
using StockDesk.Api.Dtos;

namespace StockDesk.Api.Controllers;

[ApiController]
[Route("api/stock")]
[Authorize]
public class StockController : ControllerBase
{
    private readonly StockDeskDbContext _db;

    public StockController(StockDeskDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<StockDto>>> GetStock(
        [FromQuery] int? warehouseId,
        [FromQuery] int? productId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Stocks.AsNoTracking().AsQueryable();

        if (warehouseId.HasValue)
        {
            query = query.Where(s => s.WarehouseId == warehouseId.Value);
        }

        if (productId.HasValue)
        {
            query = query.Where(s => s.ProductId == productId.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(s => s.WarehouseId).ThenBy(s => s.ProductId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StockDto(
                s.ProductId,
                s.Product!.Sku,
                s.Product.Name,
                s.WarehouseId,
                s.Warehouse!.Code,
                s.Warehouse.Name,
                s.Quantity,
                s.AverageCost))
            .ToListAsync(ct);

        return Ok(new PagedResult<StockDto>(items, page, pageSize, totalCount));
    }

    [HttpGet("low-stock")]
    public async Task<ActionResult<PagedResult<LowStockItemDto>>> GetLowStock(
        [FromQuery] int? warehouseId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Stocks.AsNoTracking()
            .Where(s => s.Quantity <= s.Product!.ReorderPoint);

        if (warehouseId.HasValue)
        {
            query = query.Where(s => s.WarehouseId == warehouseId.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(s => s.WarehouseId).ThenBy(s => s.ProductId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new LowStockItemDto(
                s.ProductId,
                s.Product!.Sku,
                s.Product.Name,
                s.WarehouseId,
                s.Warehouse!.Code,
                s.Warehouse.Name,
                s.Quantity,
                s.Product.ReorderPoint))
            .ToListAsync(ct);

        return Ok(new PagedResult<LowStockItemDto>(items, page, pageSize, totalCount));
    }
}
