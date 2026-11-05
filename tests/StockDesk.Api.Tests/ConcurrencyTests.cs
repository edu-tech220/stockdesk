using Microsoft.EntityFrameworkCore;
using StockDesk.Api.Dtos;
using StockDesk.Api.Services;
using StockDesk.Api.Tests.Infrastructure;

namespace StockDesk.Api.Tests;

/// <summary>
/// Verifica que la concurrencia optimista basada en RowVersion, combinada con el
/// retry del servicio de aplicación, evita pérdidas de actualización ("lost
/// updates") cuando varios movimientos afectan el mismo Stock simultáneamente.
/// </summary>
public class ConcurrencyTests : IDisposable
{
    private readonly SqliteDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task ConcurrentStockIn_OnSameProductWarehouse_NoLostUpdates()
    {
        int productId;
        int warehouseId;

        await using (var setupDb = _factory.CreateContext())
        {
            var product = TestDataHelper.AddProduct(setupDb);
            var warehouse = TestDataHelper.AddWarehouse(setupDb);
            productId = product.Id;
            warehouseId = warehouse.Id;
        }

        const int concurrentOperations = 10;
        const decimal quantityPerOperation = 3m;

        var tasks = Enumerable.Range(0, concurrentOperations).Select(async _ =>
        {
            // Cada operación concurrente usa su propio DbContext (misma conexión SQLite
            // subyacente), simulando llamadas concurrentes reales al servicio de aplicación.
            await using var db = _factory.CreateContext();
            var service = new MovementService(db);
            await service.RegisterInAsync(
                new StockInRequest(productId, warehouseId, quantityPerOperation, 10m, "Concurrente"),
                userId: 1);
        });

        await Task.WhenAll(tasks);

        await using var verifyDb = _factory.CreateContext();
        var stock = await verifyDb.Stocks.SingleAsync(s => s.ProductId == productId && s.WarehouseId == warehouseId);

        Assert.Equal(concurrentOperations * quantityPerOperation, stock.Quantity);

        var movementCount = await verifyDb.StockMovements.CountAsync(m => m.ProductId == productId);
        Assert.Equal(concurrentOperations, movementCount);
    }

    [Fact]
    public async Task ConcurrentStockOut_OnSameProductWarehouse_NoOverselling()
    {
        int productId;
        int warehouseId;

        await using (var setupDb = _factory.CreateContext())
        {
            var product = TestDataHelper.AddProduct(setupDb);
            var warehouse = TestDataHelper.AddWarehouse(setupDb);
            productId = product.Id;
            warehouseId = warehouse.Id;

            var service = new MovementService(setupDb);
            await service.RegisterInAsync(new StockInRequest(productId, warehouseId, 20m, 5m, null), userId: 1);
        }

        const int concurrentOperations = 10;
        const decimal quantityPerOperation = 2m;

        var tasks = Enumerable.Range(0, concurrentOperations).Select(async _ =>
        {
            await using var db = _factory.CreateContext();
            var service = new MovementService(db);
            await service.RegisterOutAsync(
                new StockOutRequest(productId, warehouseId, quantityPerOperation, "Concurrente"),
                userId: 1);
        });

        await Task.WhenAll(tasks);

        await using var verifyDb = _factory.CreateContext();
        var stock = await verifyDb.Stocks.SingleAsync(s => s.ProductId == productId && s.WarehouseId == warehouseId);

        // 20 iniciales - (10 * 2) = 0, sin pérdidas de actualización ni sobreventa.
        Assert.Equal(0m, stock.Quantity);
    }
}
