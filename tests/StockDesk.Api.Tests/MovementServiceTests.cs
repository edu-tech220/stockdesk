using Microsoft.EntityFrameworkCore;
using StockDesk.Api.Dtos;
using StockDesk.Api.Services;
using StockDesk.Api.Tests.Infrastructure;

namespace StockDesk.Api.Tests;

public class MovementServiceTests : IDisposable
{
    private readonly SqliteDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task RegisterIn_FirstEntry_SetsAverageCostToUnitCost()
    {
        await using var db = _factory.CreateContext();
        var product = TestDataHelper.AddProduct(db);
        var warehouse = TestDataHelper.AddWarehouse(db);
        var service = new MovementService(db);

        var result = await service.RegisterInAsync(
            new StockInRequest(product.Id, warehouse.Id, 10m, 5m, "Primera entrada"), userId: 1);

        Assert.Equal(5m, result.ResultingAverageCost);

        var stock = await db.Stocks.SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == warehouse.Id);
        Assert.Equal(10m, stock.Quantity);
        Assert.Equal(5m, stock.AverageCost);
    }

    [Fact]
    public async Task RegisterIn_SuccessiveEntries_ComputesWeightedAverage()
    {
        await using var db = _factory.CreateContext();
        var product = TestDataHelper.AddProduct(db);
        var warehouse = TestDataHelper.AddWarehouse(db);
        var service = new MovementService(db);

        // 10 unidades @ 5 = 50
        await service.RegisterInAsync(new StockInRequest(product.Id, warehouse.Id, 10m, 5m, null), userId: 1);
        // + 10 unidades @ 9 = 90 => total 20 unidades, 140 => promedio 7
        var second = await service.RegisterInAsync(new StockInRequest(product.Id, warehouse.Id, 10m, 9m, null), userId: 1);

        Assert.Equal(7m, second.ResultingAverageCost);

        var stock = await db.Stocks.SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == warehouse.Id);
        Assert.Equal(20m, stock.Quantity);
        Assert.Equal(7m, stock.AverageCost);
    }

    [Fact]
    public async Task RegisterOut_SufficientStock_DoesNotChangeAverageCost()
    {
        await using var db = _factory.CreateContext();
        var product = TestDataHelper.AddProduct(db);
        var warehouse = TestDataHelper.AddWarehouse(db);
        var service = new MovementService(db);

        await service.RegisterInAsync(new StockInRequest(product.Id, warehouse.Id, 10m, 5m, null), userId: 1);

        var outResult = await service.RegisterOutAsync(new StockOutRequest(product.Id, warehouse.Id, 4m, "Venta"), userId: 1);

        Assert.Equal(5m, outResult.ResultingAverageCost);

        var stock = await db.Stocks.SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == warehouse.Id);
        Assert.Equal(6m, stock.Quantity);
        Assert.Equal(5m, stock.AverageCost);
    }

    [Fact]
    public async Task RegisterOut_InsufficientStock_ThrowsBusinessRuleException409()
    {
        await using var db = _factory.CreateContext();
        var product = TestDataHelper.AddProduct(db);
        var warehouse = TestDataHelper.AddWarehouse(db);
        var service = new MovementService(db);

        await service.RegisterInAsync(new StockInRequest(product.Id, warehouse.Id, 5m, 5m, null), userId: 1);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.RegisterOutAsync(new StockOutRequest(product.Id, warehouse.Id, 10m, null), userId: 1));

        Assert.Equal(409, ex.StatusCode);

        var stock = await db.Stocks.SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == warehouse.Id);
        Assert.Equal(5m, stock.Quantity); // no cambia
    }

    [Fact]
    public async Task RegisterOut_WithoutExistingStock_ThrowsBusinessRuleException()
    {
        await using var db = _factory.CreateContext();
        var product = TestDataHelper.AddProduct(db);
        var warehouse = TestDataHelper.AddWarehouse(db);
        var service = new MovementService(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.RegisterOutAsync(new StockOutRequest(product.Id, warehouse.Id, 1m, null), userId: 1));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task RegisterTransfer_MovesStockAndRecalculatesDestinationAverage()
    {
        await using var db = _factory.CreateContext();
        var product = TestDataHelper.AddProduct(db);
        var source = TestDataHelper.AddWarehouse(db, "SRC");
        var destination = TestDataHelper.AddWarehouse(db, "DST");
        var service = new MovementService(db);

        // Origen: 10 @ 5 = 50
        await service.RegisterInAsync(new StockInRequest(product.Id, source.Id, 10m, 5m, null), userId: 1);
        // Destino ya tiene 5 unidades @ 10 = 50
        await service.RegisterInAsync(new StockInRequest(product.Id, destination.Id, 5m, 10m, null), userId: 1);

        // Transferir 5 unidades (costo origen = 5) hacia destino:
        // destino: (5*10 + 5*5) / 10 = (50+25)/10 = 7.5
        var result = await service.RegisterTransferAsync(
            new StockTransferRequest(product.Id, source.Id, destination.Id, 5m, "Reubicación"), userId: 1);

        Assert.Equal(7.5m, result.ResultingAverageCost);

        var sourceStock = await db.Stocks.SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == source.Id);
        Assert.Equal(5m, sourceStock.Quantity);
        Assert.Equal(5m, sourceStock.AverageCost); // no cambia en origen

        var destStock = await db.Stocks.SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == destination.Id);
        Assert.Equal(10m, destStock.Quantity);
        Assert.Equal(7.5m, destStock.AverageCost);
    }

    [Fact]
    public async Task RegisterTransfer_WithoutPreviousDestinationStock_SetsAverageToSourceCost()
    {
        await using var db = _factory.CreateContext();
        var product = TestDataHelper.AddProduct(db);
        var source = TestDataHelper.AddWarehouse(db, "SRC");
        var destination = TestDataHelper.AddWarehouse(db, "DST");
        var service = new MovementService(db);

        await service.RegisterInAsync(new StockInRequest(product.Id, source.Id, 10m, 8m, null), userId: 1);

        var result = await service.RegisterTransferAsync(
            new StockTransferRequest(product.Id, source.Id, destination.Id, 10m, null), userId: 1);

        Assert.Equal(8m, result.ResultingAverageCost);

        var destStock = await db.Stocks.SingleAsync(s => s.ProductId == product.Id && s.WarehouseId == destination.Id);
        Assert.Equal(10m, destStock.Quantity);
        Assert.Equal(8m, destStock.AverageCost);
    }

    [Fact]
    public async Task RegisterTransfer_InsufficientSourceStock_ThrowsBusinessRuleException()
    {
        await using var db = _factory.CreateContext();
        var product = TestDataHelper.AddProduct(db);
        var source = TestDataHelper.AddWarehouse(db, "SRC");
        var destination = TestDataHelper.AddWarehouse(db, "DST");
        var service = new MovementService(db);

        await service.RegisterInAsync(new StockInRequest(product.Id, source.Id, 2m, 5m, null), userId: 1);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.RegisterTransferAsync(
                new StockTransferRequest(product.Id, source.Id, destination.Id, 10m, null), userId: 1));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task RegisterTransfer_SameSourceAndDestination_ThrowsBusinessRuleException()
    {
        await using var db = _factory.CreateContext();
        var product = TestDataHelper.AddProduct(db);
        var warehouse = TestDataHelper.AddWarehouse(db);
        var service = new MovementService(db);

        await service.RegisterInAsync(new StockInRequest(product.Id, warehouse.Id, 10m, 5m, null), userId: 1);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.RegisterTransferAsync(
                new StockTransferRequest(product.Id, warehouse.Id, warehouse.Id, 1m, null), userId: 1));

        Assert.Equal(400, ex.StatusCode);
    }
}
