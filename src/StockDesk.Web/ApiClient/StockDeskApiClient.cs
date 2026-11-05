using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using StockDesk.Web.Models;

namespace StockDesk.Web.ApiClient;

public class StockDeskApiClient : IStockDeskApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public StockDeskApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<LoginResponse?> LoginAsync(string username, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/login", new LoginRequest(username, password));

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
    }

    public async Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(string jwt)
    {
        var request = CreateRequest(HttpMethod.Get, "api/warehouses", jwt);
        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<WarehouseDto>>(JsonOptions) ?? [];
    }

    public async Task<WarehouseDto> CreateWarehouseAsync(CreateWarehouseRequest request, string jwt)
    {
        var httpRequest = CreateRequest(HttpMethod.Post, "api/warehouses", jwt, request);
        var response = await _httpClient.SendAsync(httpRequest);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<WarehouseDto>(JsonOptions))!;
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(string jwt)
    {
        var request = CreateRequest(HttpMethod.Get, "api/products", jwt);
        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<ProductDto>>(JsonOptions) ?? [];
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, string jwt)
    {
        var httpRequest = CreateRequest(HttpMethod.Post, "api/products", jwt, request);
        var response = await _httpClient.SendAsync(httpRequest);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<ProductDto>(JsonOptions))!;
    }

    public async Task<PagedResult<StockDto>> GetStockAsync(string jwt, int? warehouseId, int? productId, int page, int pageSize)
    {
        var query = BuildQuery(new Dictionary<string, object?>
        {
            ["warehouseId"] = warehouseId,
            ["productId"] = productId,
            ["page"] = page,
            ["pageSize"] = pageSize
        });

        var request = CreateRequest(HttpMethod.Get, $"api/stock{query}", jwt);
        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<PagedResult<StockDto>>(JsonOptions))!;
    }

    public async Task<PagedResult<LowStockItemDto>> GetLowStockAsync(string jwt, int? warehouseId, int page, int pageSize)
    {
        var query = BuildQuery(new Dictionary<string, object?>
        {
            ["warehouseId"] = warehouseId,
            ["page"] = page,
            ["pageSize"] = pageSize
        });

        var request = CreateRequest(HttpMethod.Get, $"api/stock/low-stock{query}", jwt);
        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<PagedResult<LowStockItemDto>>(JsonOptions))!;
    }

    public async Task<MovementDto> RegisterInAsync(StockInRequest request, string jwt)
    {
        var httpRequest = CreateRequest(HttpMethod.Post, "api/movements/in", jwt, request);
        var response = await _httpClient.SendAsync(httpRequest);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<MovementDto>(JsonOptions))!;
    }

    public async Task<MovementDto> RegisterOutAsync(StockOutRequest request, string jwt)
    {
        var httpRequest = CreateRequest(HttpMethod.Post, "api/movements/out", jwt, request);
        var response = await _httpClient.SendAsync(httpRequest);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<MovementDto>(JsonOptions))!;
    }

    public async Task<MovementDto> RegisterTransferAsync(StockTransferRequest request, string jwt)
    {
        var httpRequest = CreateRequest(HttpMethod.Post, "api/movements/transfer", jwt, request);
        var response = await _httpClient.SendAsync(httpRequest);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<MovementDto>(JsonOptions))!;
    }

    public async Task<PagedResult<MovementDto>> GetMovementsAsync(
        string jwt, int? productId, int? warehouseId, DateTime? from, DateTime? to, int page, int pageSize)
    {
        var query = BuildQuery(new Dictionary<string, object?>
        {
            ["productId"] = productId,
            ["warehouseId"] = warehouseId,
            ["from"] = from?.ToString("O"),
            ["to"] = to?.ToString("O"),
            ["page"] = page,
            ["pageSize"] = pageSize
        });

        var request = CreateRequest(HttpMethod.Get, $"api/movements{query}", jwt);
        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<PagedResult<MovementDto>>(JsonOptions))!;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string jwt, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private static string BuildQuery(Dictionary<string, object?> parameters)
    {
        var parts = parameters
            .Where(p => p.Value is not null)
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!.ToString()!)}");

        var joined = string.Join("&", parts);
        return string.IsNullOrEmpty(joined) ? string.Empty : $"?{joined}";
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string detail = $"Error de la API ({(int)response.StatusCode}).";

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>(JsonOptions);
            if (problem is not null && !string.IsNullOrWhiteSpace(problem.Detail))
            {
                detail = problem.Detail;
            }
        }
        catch
        {
            // Ignorar errores de deserialización del cuerpo de error; se usa el mensaje genérico.
        }

        throw new StockDeskApiException(detail, (int)response.StatusCode);
    }
}
