using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockDesk.Api.Data;
using StockDesk.Api.Domain;
using StockDesk.Api.Dtos;
using StockDesk.Api.Services;

namespace StockDesk.Api.Controllers;

[ApiController]
[Route("api/warehouses")]
[Authorize]
public class WarehousesController : ControllerBase
{
    private readonly StockDeskDbContext _db;

    public WarehousesController(StockDeskDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WarehouseDto>>> GetAll(CancellationToken ct)
    {
        var warehouses = await _db.Warehouses
            .OrderBy(w => w.Code)
            .Select(w => new WarehouseDto(w.Id, w.Code, w.Name, w.Location, w.IsActive))
            .ToListAsync(ct);

        return Ok(warehouses);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<WarehouseDto>> Create(CreateWarehouseRequest request, CancellationToken ct)
    {
        if (await _db.Warehouses.AnyAsync(w => w.Code == request.Code, ct))
        {
            throw new BusinessRuleException("Ya existe un almacén con ese código.", statusCode: 409);
        }

        var warehouse = new Warehouse
        {
            Code = request.Code,
            Name = request.Name,
            Location = request.Location ?? string.Empty,
            IsActive = true
        };

        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync(ct);

        var dto = new WarehouseDto(warehouse.Id, warehouse.Code, warehouse.Name, warehouse.Location, warehouse.IsActive);
        return CreatedAtAction(nameof(GetAll), dto);
    }
}
