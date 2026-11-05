using Microsoft.EntityFrameworkCore;
using StockDesk.Api.Domain;

namespace StockDesk.Api.Data;

public class StockDeskDbContext : DbContext
{
    public StockDeskDbContext(DbContextOptions<StockDeskDbContext> options) : base(options)
    {
    }

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.HasIndex(w => w.Code).IsUnique();
            entity.Property(w => w.Code).HasMaxLength(32).IsRequired();
            entity.Property(w => w.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(p => p.Sku).IsUnique();
            entity.Property(p => p.Sku).HasMaxLength(64).IsRequired();
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Unit).HasMaxLength(32).IsRequired();
        });

        modelBuilder.Entity<Stock>(entity =>
        {
            entity.HasIndex(s => new { s.ProductId, s.WarehouseId }).IsUnique();

            entity.Property(s => s.Quantity).HasColumnType("decimal(18,4)");
            entity.Property(s => s.AverageCost).HasColumnType("decimal(18,4)");

            entity.HasOne(s => s.Product)
                .WithMany()
                .HasForeignKey(s => s.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Warehouse)
                .WithMany()
                .HasForeignKey(s => s.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            // En SQL Server, RowVersion se mapea a rowversion real vía [Timestamp]
            // (columna rowversion/timestamp generada y actualizada por el motor de
            // base de datos). SQLite no tiene un tipo rowversion nativo, así que en
            // ese proveedor (usado solo por las pruebas) el valor se simula de forma
            // manual: se asigna un nuevo token de concurrencia en cada inserción o
            // actualización desde SaveChanges/SaveChangesAsync (ver más abajo), en
            // lugar de depender de un ValueGenerator plugificado en el modelo.
            if (Database.IsSqlite())
            {
                // Se desactiva la generación de valor automática que agrega la
                // convención [Timestamp] (pensada para `rowversion` de SQL Server).
                // En SQLite el valor se asigna manualmente desde la aplicación
                // (ver StampSqliteRowVersions), por lo que el valor puesto en la
                // entidad debe viajar tal cual al INSERT/UPDATE.
                entity.Property(s => s.RowVersion)
                    .IsConcurrencyToken()
                    .ValueGeneratedNever();
            }
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.Property(m => m.Quantity).HasColumnType("decimal(18,4)");
            entity.Property(m => m.UnitCost).HasColumnType("decimal(18,4)");
            entity.Property(m => m.ResultingAverageCost).HasColumnType("decimal(18,4)");
            entity.Property(m => m.Note).HasMaxLength(500);

            entity.HasOne(m => m.Product)
                .WithMany()
                .HasForeignKey(m => m.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.Warehouse)
                .WithMany()
                .HasForeignKey(m => m.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.DestinationWarehouse)
                .WithMany()
                .HasForeignKey(m => m.DestinationWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(m => new { m.ProductId, m.WarehouseId, m.CreatedAtUtc });
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Username).IsUnique();
            entity.Property(u => u.Username).HasMaxLength(100).IsRequired();
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampSqliteRowVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampSqliteRowVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// En SQLite, genera manualmente un nuevo token de concurrencia para cada
    /// Stock agregado o modificado antes de guardar, simulando el comportamiento
    /// de `rowversion` de SQL Server (que el motor actualiza automáticamente).
    /// </summary>
    private void StampSqliteRowVersions()
    {
        if (!Database.IsSqlite())
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<Stock>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.RowVersion = Guid.NewGuid().ToByteArray()[..8];
            }
        }
    }
}
