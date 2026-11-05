using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StockDesk.Web.ApiClient;
using StockDesk.Web.Models;

namespace StockDesk.Web.Pages.Kardex;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IStockDeskApiClient _apiClient;

    public IndexModel(IStockDeskApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty(SupportsGet = true)]
    public int? ProductId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? WarehouseId { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    public IReadOnlyList<ProductDto> Products { get; set; } = [];
    public IReadOnlyList<WarehouseDto> Warehouses { get; set; } = [];
    public PagedResult<MovementDto> Result { get; set; } = new([], 1, 20, 0);
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        var jwt = User.GetJwt();
        if (string.IsNullOrEmpty(jwt))
        {
            return;
        }

        try
        {
            Products = await _apiClient.GetProductsAsync(jwt);
            Warehouses = await _apiClient.GetWarehousesAsync(jwt);

            if (ProductId.HasValue)
            {
                Result = await _apiClient.GetMovementsAsync(jwt, ProductId, WarehouseId, null, null, PageNumber, 20);
            }
        }
        catch (StockDeskApiException ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
