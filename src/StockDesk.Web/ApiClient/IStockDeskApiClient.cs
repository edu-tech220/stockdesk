using StockDesk.Web.Models;

namespace StockDesk.Web.ApiClient;

/// <summary>
/// Cliente tipado hacia el backend StockDesk.Api. Todas las llamadas HTTP se hacen
/// desde el servidor (Razor Page handler) usando IHttpClientFactory; el navegador
/// nunca llama directamente a la API.
/// </summary>
public interface IStockDeskApiClient
{
    Task<LoginResponse?> LoginAsync(string username, string password);

    Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(string jwt);
    Task<WarehouseDto> CreateWarehouseAsync(CreateWarehouseRequest request, string jwt);

    Task<IReadOnlyList<ProductDto>> GetProductsAsync(string jwt);
    Task<ProductDto> CreateProductAsync(CreateProductRequest request, string jwt);

    Task<PagedResult<StockDto>> GetStockAsync(string jwt, int? warehouseId, int? productId, int page, int pageSize);
    Task<PagedResult<LowStockItemDto>> GetLowStockAsync(string jwt, int? warehouseId, int page, int pageSize);

    Task<MovementDto> RegisterInAsync(StockInRequest request, string jwt);
    Task<MovementDto> RegisterOutAsync(StockOutRequest request, string jwt);
    Task<MovementDto> RegisterTransferAsync(StockTransferRequest request, string jwt);

    Task<PagedResult<MovementDto>> GetMovementsAsync(
        string jwt, int? productId, int? warehouseId, DateTime? from, DateTime? to, int page, int pageSize);
}
