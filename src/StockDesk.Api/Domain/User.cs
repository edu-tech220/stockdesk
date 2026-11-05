namespace StockDesk.Api.Domain;

public enum UserRole
{
    Admin = 0,
    Operator = 1
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;

    /// <summary>Hash PBKDF2 (Base64) de la contraseña.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Salt aleatorio (Base64) usado para derivar el hash PBKDF2.</summary>
    public string PasswordSalt { get; set; } = string.Empty;

    public UserRole Role { get; set; }
}
