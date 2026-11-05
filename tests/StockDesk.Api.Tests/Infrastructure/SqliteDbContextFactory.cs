using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StockDesk.Api.Data;

namespace StockDesk.Api.Tests.Infrastructure;

/// <summary>
/// Crea un StockDeskDbContext respaldado por SQLite in-memory, manteniendo una
/// única conexión abierta durante toda la vida de la prueba (requerido por SQLite
/// para que la base de datos en memoria persista entre operaciones). No requiere
/// SQL Server ni Docker.
/// </summary>
public sealed class SqliteDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<StockDeskDbContext> _options;

    public SqliteDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<StockDeskDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public StockDeskDbContext CreateContext() => new(_options);

    public void Dispose()
    {
        _connection.Dispose();
    }
}
