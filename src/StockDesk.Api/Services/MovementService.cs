using Microsoft.EntityFrameworkCore;
using StockDesk.Api.Data;
using StockDesk.Api.Domain;
using StockDesk.Api.Dtos;

namespace StockDesk.Api.Services;

public interface IMovementService
{
    Task<MovementDto> RegisterInAsync(StockInRequest request, int userId, CancellationToken ct = default);
    Task<MovementDto> RegisterOutAsync(StockOutRequest request, int userId, CancellationToken ct = default);
    Task<MovementDto> RegisterTransferAsync(StockTransferRequest request, int userId, CancellationToken ct = default);
}

/// <summary>
/// Orquesta los movimientos de inventario aplicando las reglas de costeo
/// promedio ponderado y manejando la concurrencia optimista sobre Stock
/// mediante reintentos (patrón "read-modify-write" con retry en
/// DbUpdateConcurrencyException).
/// </summary>
public class MovementService : IMovementService
{
    private const int MaxRetries = 3;

    private readonly StockDeskDbContext _db;

    public MovementService(StockDeskDbContext db)
    {
        _db = db;
    }

    public async Task<MovementDto> RegisterInAsync(StockInRequest request, int userId, CancellationToken ct = default)
    {
        if (request.Quantity <= 0)
        {
            throw new BusinessRuleException("La cantidad debe ser mayor que cero.");
        }

        if (request.UnitCost < 0)
        {
            throw new BusinessRuleException("El costo unitario no puede ser negativo.");
        }

        return await ExecuteWithRetryAsync(async () =>
        {
            var product = await GetActiveProductAsync(request.ProductId, ct);
            var warehouse = await GetActiveWarehouseAsync(request.WarehouseId, ct);

            var stock = await GetOrCreateStockAsync(request.ProductId, request.WarehouseId, ct);

            var currentQty = stock.Quantity;
            var currentAvg = stock.AverageCost;
            var newQty = currentQty + request.Quantity;
            var newAvg = newQty == 0
                ? 0
                : (currentQty * currentAvg + request.Quantity * request.UnitCost) / newQty;

            stock.Quantity = newQty;
            stock.AverageCost = newAvg;

            var movement = new StockMovement
            {
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                Type = MovementType.In,
                Quantity = request.Quantity,
                UnitCost = request.UnitCost,
                ResultingAverageCost = newAvg,
                Note = request.Note,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = userId
            };

            _db.StockMovements.Add(movement);
            await _db.SaveChangesAsync(ct);

            return ToDto(movement, product, warehouse, null);
        }, ct);
    }

    public async Task<MovementDto> RegisterOutAsync(StockOutRequest request, int userId, CancellationToken ct = default)
    {
        if (request.Quantity <= 0)
        {
            throw new BusinessRuleException("La cantidad debe ser mayor que cero.");
        }

        return await ExecuteWithRetryAsync(async () =>
        {
            var product = await GetActiveProductAsync(request.ProductId, ct);
            var warehouse = await GetActiveWarehouseAsync(request.WarehouseId, ct);

            var stock = await _db.Stocks
                .FirstOrDefaultAsync(s => s.ProductId == request.ProductId && s.WarehouseId == request.WarehouseId, ct);

            if (stock is null || stock.Quantity < request.Quantity)
            {
                throw new BusinessRuleException(
                    $"Stock insuficiente. Disponible: {(stock?.Quantity ?? 0)}, solicitado: {request.Quantity}.",
                    statusCode: 409);
            }

            stock.Quantity -= request.Quantity;
            // El costo promedio NO cambia en una salida.

            var movement = new StockMovement
            {
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                Type = MovementType.Out,
                Quantity = request.Quantity,
                UnitCost = null,
                ResultingAverageCost = stock.AverageCost,
                Note = request.Note,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = userId
            };

            _db.StockMovements.Add(movement);
            await _db.SaveChangesAsync(ct);

            return ToDto(movement, product, warehouse, null);
        }, ct);
    }

    public async Task<MovementDto> RegisterTransferAsync(StockTransferRequest request, int userId, CancellationToken ct = default)
    {
        if (request.Quantity <= 0)
        {
            throw new BusinessRuleException("La cantidad debe ser mayor que cero.");
        }

        if (request.SourceWarehouseId == request.DestinationWarehouseId)
        {
            throw new BusinessRuleException("El almacén origen y destino no pueden ser el mismo.");
        }

        return await ExecuteWithRetryAsync(async () =>
        {
            var product = await GetActiveProductAsync(request.ProductId, ct);
            var sourceWarehouse = await GetActiveWarehouseAsync(request.SourceWarehouseId, ct);
            var destinationWarehouse = await GetActiveWarehouseAsync(request.DestinationWarehouseId, ct);

            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            var sourceStock = await _db.Stocks
                .FirstOrDefaultAsync(s => s.ProductId == request.ProductId && s.WarehouseId == request.SourceWarehouseId, ct);

            if (sourceStock is null || sourceStock.Quantity < request.Quantity)
            {
                throw new BusinessRuleException(
                    $"Stock insuficiente en el almacén origen. Disponible: {(sourceStock?.Quantity ?? 0)}, solicitado: {request.Quantity}.",
                    statusCode: 409);
            }

            var sourceAvgCost = sourceStock.AverageCost;
            sourceStock.Quantity -= request.Quantity;
            // El costo promedio del origen no cambia.

            var destinationStock = await GetOrCreateStockAsync(request.ProductId, request.DestinationWarehouseId, ct);

            var destCurrentQty = destinationStock.Quantity;
            var destCurrentAvg = destinationStock.AverageCost;
            var destNewQty = destCurrentQty + request.Quantity;
            var destNewAvg = destNewQty == 0
                ? 0
                : (destCurrentQty * destCurrentAvg + request.Quantity * sourceAvgCost) / destNewQty;

            destinationStock.Quantity = destNewQty;
            destinationStock.AverageCost = destNewAvg;

            var movement = new StockMovement
            {
                ProductId = product.Id,
                WarehouseId = sourceWarehouse.Id,
                DestinationWarehouseId = destinationWarehouse.Id,
                Type = MovementType.Transfer,
                Quantity = request.Quantity,
                UnitCost = sourceAvgCost,
                ResultingAverageCost = destNewAvg,
                Note = request.Note,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = userId
            };

            _db.StockMovements.Add(movement);
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return ToDto(movement, product, sourceWarehouse, destinationWarehouse);
        }, ct);
    }

    /// <summary>
    /// Ejecuta la operación completa (lectura + recálculo + escritura) con reintentos
    /// ante DbUpdateConcurrencyException, releyendo el estado actual de Stock en cada
    /// intento. Tras agotar los intentos, lanza ConcurrencyConflictException (409).
    /// </summary>
    private async Task<MovementDto> ExecuteWithRetryAsync(Func<Task<MovementDto>> operation, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (DbUpdateConcurrencyException)
            {
                foreach (var entry in _db.ChangeTracker.Entries().ToList())
                {
                    entry.State = EntityState.Detached;
                }

                if (attempt == MaxRetries)
                {
                    throw new ConcurrencyConflictException(
                        "No se pudo completar la operación debido a modificaciones concurrentes sobre el mismo stock. Intente nuevamente.");
                }
            }
        }

        // Inalcanzable, pero requerido por el compilador.
        throw new ConcurrencyConflictException("No se pudo completar la operación.");
    }

    private async Task<Stock> GetOrCreateStockAsync(int productId, int warehouseId, CancellationToken ct)
    {
        var stock = await _db.Stocks
            .FirstOrDefaultAsync(s => s.ProductId == productId && s.WarehouseId == warehouseId, ct);

        if (stock is not null)
        {
            return stock;
        }

        stock = new Stock
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Quantity = 0,
            AverageCost = 0
        };

        _db.Stocks.Add(stock);
        // Se persiste de inmediato para obtener Id/RowVersion y evitar duplicados
        // por el índice único (ProductId, WarehouseId) dentro de la misma operación.
        await _db.SaveChangesAsync(ct);

        return stock;
    }

    private async Task<Product> GetActiveProductAsync(int productId, CancellationToken ct)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null || !product.IsActive)
        {
            throw new BusinessRuleException("El producto no existe o está inactivo.", statusCode: 400);
        }

        return product;
    }

    private async Task<Warehouse> GetActiveWarehouseAsync(int warehouseId, CancellationToken ct)
    {
        var warehouse = await _db.Warehouses.FirstOrDefaultAsync(w => w.Id == warehouseId, ct);
        if (warehouse is null || !warehouse.IsActive)
        {
            throw new BusinessRuleException("El almacén no existe o está inactivo.", statusCode: 400);
        }

        return warehouse;
    }

    private static MovementDto ToDto(StockMovement movement, Product product, Warehouse warehouse, Warehouse? destination)
    {
        return new MovementDto(
            movement.Id,
            product.Id,
            product.Sku,
            product.Name,
            warehouse.Id,
            warehouse.Name,
            destination?.Id,
            destination?.Name,
            movement.Type,
            movement.Quantity,
            movement.UnitCost,
            movement.ResultingAverageCost,
            movement.Note,
            movement.CreatedAtUtc,
            movement.CreatedByUserId);
    }
}
