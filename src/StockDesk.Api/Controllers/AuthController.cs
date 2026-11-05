using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockDesk.Api.Dtos;
using StockDesk.Api.Services;

namespace StockDesk.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("users")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> CreateUser(CreateUserRequest request, CancellationToken ct)
    {
        var user = await _authService.CreateUserAsync(request, ct);
        return CreatedAtAction(nameof(Login), new { }, new { user.Id, user.Username, Role = user.Role.ToString() });
    }
}
