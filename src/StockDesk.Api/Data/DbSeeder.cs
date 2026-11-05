using Microsoft.EntityFrameworkCore;
using StockDesk.Api.Auth;
using StockDesk.Api.Domain;

namespace StockDesk.Api.Data;

/// <summary>
/// Crea el esquema (si no existe) e inserta un usuario administrador inicial
/// para desarrollo/pruebas. Las credenciales de ejemplo (admin / Admin123!)
/// NO deben usarse en producción: cámbielas inmediatamente tras el primer login.
/// </summary>
public static class DbSeeder
{
    public const string DefaultAdminUsername = "admin";
    public const string DefaultAdminPassword = "Admin123!";

    public static async Task SeedAsync(StockDeskDbContext db)
    {
        if (!await db.Users.AnyAsync())
        {
            var (hash, salt) = PasswordHasher.HashPassword(DefaultAdminPassword);
            db.Users.Add(new User
            {
                Username = DefaultAdminUsername,
                PasswordHash = hash,
                PasswordSalt = salt,
                Role = UserRole.Admin
            });

            await db.SaveChangesAsync();
        }
    }
}
