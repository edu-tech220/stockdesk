using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockDesk.Api.Data;
using StockDesk.Api.Domain;
using StockDesk.Api.Dtos;
using StockDesk.Api.Services;

namespace StockDesk.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly StockDeskDbContext _db;

    public ProductsController(StockDeskDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetAll(CancellationToken ct)
    {
        var products = await _db.Products
            .OrderBy(p => p.Sku)
            .Select(p => new ProductDto(p.Id, p.Sku, p.Name, p.Description, p.Unit, p.ReorderPoint, p.IsActive))
            .ToListAsync(ct);

        return Ok(products);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDto>> Create(CreateProductRequest request, CancellationToken ct)
    {
        if (await _db.Products.AnyAsync(p => p.Sku == request.Sku, ct))
        {
            throw new BusinessRuleException("Ya existe un producto con ese SKU.", statusCode: 409);
        }

        var product = new Product
        {
            Sku = request.Sku,
            Name = request.Name,
            Description = request.Description,
            Unit = request.Unit,
            ReorderPoint = request.ReorderPoint,
            IsActive = true
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);

        var dto = new ProductDto(product.Id, product.Sku, product.Name, product.Description, product.Unit, product.ReorderPoint, product.IsActive);
        return CreatedAtAction(nameof(GetAll), dto);
    }
}
