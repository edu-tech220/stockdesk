namespace StockDesk.Api.Services;

/// <summary>
/// Excepción para violaciones de reglas de negocio que deben traducirse
/// a respuestas 400/409 con ProblemDetails.
/// </summary>
public class BusinessRuleException : Exception
{
    public int StatusCode { get; }

    public BusinessRuleException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }
}

public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message) : base(message)
    {
    }
}
