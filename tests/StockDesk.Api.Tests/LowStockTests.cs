using Microsoft.EntityFrameworkCore;
using StockDesk.Api.Tests.Infrastructure;

namespace StockDesk.Api.Tests;

public class LowStockTests : IDisposable
{
    private readonly SqliteDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task LowStockQuery_ReturnsOnlyProductsAtOrBelowReorderPoint()
    {
        await using var db = _factory.CreateContext();
        var warehouse = TestDataHelper.AddWarehouse(db);

        var lowProduct = TestDataHelper.AddProduct(db, "LOW-1", reorderPoint: 10);
        var okProduct = TestDataHelper.AddProduct(db, "OK-1", reorderPoint: 5);
        var exactProduct = TestDataHelper.AddProduct(db, "EXACT-1", reorderPoint: 8);

        db.Stocks.AddRange(
            new Api.Domain.Stock { ProductId = lowProduct.Id, WarehouseId = warehouse.Id, Quantity = 3, AverageCost = 1 },
            new Api.Domain.Stock { ProductId = okProduct.Id, WarehouseId = warehouse.Id, Quantity = 50, AverageCost = 1 },
            new Api.Domain.Stock { ProductId = exactProduct.Id, WarehouseId = warehouse.Id, Quantity = 8, AverageCost = 1 });
        await db.SaveChangesAsync();

        var lowStockItems = await db.Stocks
            .Where(s => s.Quantity <= s.Product!.ReorderPoint)
            .Select(s => s.ProductId)
            .ToListAsync();

        Assert.Contains(lowProduct.Id, lowStockItems);
        Assert.Contains(exactProduct.Id, lowStockItems); // igual al punto de reorden también alerta
        Assert.DoesNotContain(okProduct.Id, lowStockItems);
        Assert.Equal(2, lowStockItems.Count);
    }
}
