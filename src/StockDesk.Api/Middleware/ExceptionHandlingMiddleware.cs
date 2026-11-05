using Microsoft.AspNetCore.Mvc;
using StockDesk.Api.Services;

namespace StockDesk.Api.Middleware;

/// <summary>
/// Traduce excepciones de negocio y de concurrencia a respuestas ProblemDetails
/// estándar de ASP.NET Core, evitando duplicar try/catch en cada controlador.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BusinessRuleException ex)
        {
            await WriteProblemAsync(context, ex.StatusCode, "Regla de negocio violada", ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Conflicto de concurrencia", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado procesando {Path}", context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "Error interno del servidor",
                "Ocurrió un error inesperado procesando la solicitud.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string title, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(problem);
    }
}
