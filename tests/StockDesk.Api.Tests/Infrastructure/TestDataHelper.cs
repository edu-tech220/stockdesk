using StockDesk.Api.Data;
using StockDesk.Api.Domain;

namespace StockDesk.Api.Tests.Infrastructure;

public static class TestDataHelper
{
    public static Product AddProduct(StockDeskDbContext db, string sku = "SKU-1", int reorderPoint = 10)
    {
        var product = new Product
        {
            Sku = sku,
            Name = $"Producto {sku}",
            Unit = "pieza",
            ReorderPoint = reorderPoint,
            IsActive = true
        };

        db.Products.Add(product);
        db.SaveChanges();
        return product;
    }

    public static Warehouse AddWarehouse(StockDeskDbContext db, string code = "WH-1")
    {
        var warehouse = new Warehouse
        {
            Code = code,
            Name = $"Almacén {code}",
            Location = "Zona industrial",
            IsActive = true
        };

        db.Warehouses.Add(warehouse);
        db.SaveChanges();
        return warehouse;
    }
}
