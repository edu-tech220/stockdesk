using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StockDesk.Web.ApiClient;
using StockDesk.Web.Models;

namespace StockDesk.Web.Pages.LowStock;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IStockDeskApiClient _apiClient;

    public IndexModel(IStockDeskApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty(SupportsGet = true)]
    public int? WarehouseId { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    public IReadOnlyList<WarehouseDto> Warehouses { get; set; } = [];
    public PagedResult<LowStockItemDto> Result { get; set; } = new([], 1, 20, 0);
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
            Warehouses = await _apiClient.GetWarehousesAsync(jwt);
            Result = await _apiClient.GetLowStockAsync(jwt, WarehouseId, PageNumber, 20);
        }
        catch (StockDeskApiException ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
