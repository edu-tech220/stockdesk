using System.ComponentModel.DataAnnotations;

namespace StockDesk.Api.Dtos;

public class LoginRequest
{
    public LoginRequest()
    {
    }

    public LoginRequest(string username, string password)
    {
        Username = username;
        Password = password;
    }

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public record LoginResponse(string Token, string Username, string Role, DateTime ExpiresAtUtc);

public class CreateUserRequest
{
    public CreateUserRequest()
    {
    }

    public CreateUserRequest(string username, string password, string role)
    {
        Username = username;
        Password = password;
        Role = role;
    }

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;
}
