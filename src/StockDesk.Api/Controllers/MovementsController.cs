using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockDesk.Api.Data;
using StockDesk.Api.Dtos;
using StockDesk.Api.Services;

namespace StockDesk.Api.Controllers;

[ApiController]
[Route("api/movements")]
[Authorize]
public class MovementsController : ControllerBase
{
    private readonly IMovementService _movementService;
    private readonly StockDeskDbContext _db;

    public MovementsController(IMovementService movementService, StockDeskDbContext db)
    {
        _movementService = movementService;
        _db = db;
    }

    [HttpPost("in")]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<ActionResult<MovementDto>> In(StockInRequest request, CancellationToken ct)
    {
        var result = await _movementService.RegisterInAsync(request, GetUserId(), ct);
        return Ok(result);
    }

    [HttpPost("out")]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<ActionResult<MovementDto>> Out(StockOutRequest request, CancellationToken ct)
    {
        var result = await _movementService.RegisterOutAsync(request, GetUserId(), ct);
        return Ok(result);
    }

    [HttpPost("transfer")]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<ActionResult<MovementDto>> Transfer(StockTransferRequest request, CancellationToken ct)
    {
        var result = await _movementService.RegisterTransferAsync(request, GetUserId(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Kardex (historial de movimientos) paginado. Proyecta directamente con Select
    /// (sin cargar entidades completas ni usar Include) para evitar N+1 al traer
    /// nombre de producto/almacén.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<MovementDto>>> GetHistory(
        [FromQuery] int? productId,
        [FromQuery] int? warehouseId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.StockMovements.AsNoTracking().AsQueryable();

        if (productId.HasValue)
        {
            query = query.Where(m => m.ProductId == productId.Value);
        }

        if (warehouseId.HasValue)
        {
            query = query.Where(m => m.WarehouseId == warehouseId.Value || m.DestinationWarehouseId == warehouseId.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(m => m.CreatedAtUtc >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(m => m.CreatedAtUtc <= to.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .ThenByDescending(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MovementDto(
                m.Id,
                m.ProductId,
                m.Product!.Sku,
                m.Product.Name,
                m.WarehouseId,
                m.Warehouse!.Name,
                m.DestinationWarehouseId,
                m.DestinationWarehouse != null ? m.DestinationWarehouse.Name : null,
                m.Type,
                m.Quantity,
                m.UnitCost,
                m.ResultingAverageCost,
                m.Note,
                m.CreatedAtUtc,
                m.CreatedByUserId))
            .ToListAsync(ct);

        return Ok(new PagedResult<MovementDto>(items, page, pageSize, totalCount));
    }

    private int GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : 0;
    }
}
