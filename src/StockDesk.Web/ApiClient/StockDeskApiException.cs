namespace StockDesk.Web.ApiClient;

/// <summary>
/// Excepción lanzada cuando la API responde con un ProblemDetails de error.
/// Se usa para mostrar el mensaje de negocio al usuario en la vista Razor.
/// </summary>
public class StockDeskApiException : Exception
{
    public int StatusCode { get; }

    public StockDeskApiException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}
