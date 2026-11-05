using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StockDesk.Api.Auth;
using StockDesk.Api.Data;
using StockDesk.Api.Middleware;
using StockDesk.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Opciones de JWT ----
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

// ---- DbContext ----
// Proveedor SQL Server para desarrollo/producción/Docker. Los tests de integración
// sobreescriben este registro para usar SQLite in-memory (ver WebApplicationFactory
// en StockDesk.Api.Tests).
var connectionString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<StockDeskDbContext>(options =>
    options.UseSqlServer(connectionString));

// ---- Servicios de aplicación ----
builder.Services.AddScoped<IMovementService, MovementService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// ---- Autenticación / Autorización ----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "StockDesk API", Version = "v1" });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Scheme = "bearer",
        BearerFormat = "JWT",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Description = "Ingrese el token JWT recibido de /api/auth/login.",
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };

    options.AddSecurityDefinition(jwtScheme.Reference.Id, jwtScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtScheme, Array.Empty<string>() }
    });
});

var app = builder.Build();

// ---- Migraciones/EnsureCreated + seed en arranque ----
// En SQL Server (desarrollo/Docker/producción) se aplican las migraciones reales
// de EF Core generadas con `dotnet ef migrations add`. En SQLite (usado solo por
// las pruebas de integración vía WebApplicationFactory) se usa EnsureCreated,
// ya que las migraciones de este proyecto están generadas para el proveedor
// SQL Server y no son compatibles con SQLite.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StockDeskDbContext>();
    if (db.Database.IsSqlServer())
    {
        db.Database.Migrate();
    }
    else
    {
        db.Database.EnsureCreated();
    }

    await DbSeeder.SeedAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

/// <summary>
/// Clase parcial pública requerida para que WebApplicationFactory&lt;Program&gt;
/// pueda referenciar el entry point desde el proyecto de pruebas.
/// </summary>
public partial class Program
{
}
