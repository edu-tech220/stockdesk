using Microsoft.EntityFrameworkCore;
using StockDesk.Api.Auth;
using StockDesk.Api.Data;
using StockDesk.Api.Domain;
using StockDesk.Api.Dtos;

namespace StockDesk.Api.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<User> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly StockDeskDbContext _db;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly JwtOptions _jwtOptions;

    public AuthService(StockDeskDbContext db, IJwtTokenGenerator tokenGenerator, JwtOptions jwtOptions)
    {
        _db = db;
        _tokenGenerator = tokenGenerator;
        _jwtOptions = jwtOptions;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username, ct);

        if (user is null || !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            throw new BusinessRuleException("Usuario o contraseña incorrectos.", statusCode: 401);
        }

        var token = _tokenGenerator.GenerateToken(user);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);

        return new LoginResponse(token, user.Username, user.Role.ToString(), expiresAt);
    }

    public async Task<User> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        if (await _db.Users.AnyAsync(u => u.Username == request.Username, ct))
        {
            throw new BusinessRuleException("El nombre de usuario ya existe.", statusCode: 409);
        }

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
        {
            throw new BusinessRuleException("Rol inválido. Use 'Admin' u 'Operator'.");
        }

        var (hash, salt) = PasswordHasher.HashPassword(request.Password);

        var user = new User
        {
            Username = request.Username,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return user;
    }
}
