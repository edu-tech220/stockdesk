using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StockDesk.Web.ApiClient;
using StockDesk.Web.Models;

namespace StockDesk.Web.Pages.Movements;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IStockDeskApiClient _apiClient;

    public IndexModel(IStockDeskApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty(SupportsGet = true)]
    public string Tab { get; set; } = "in";

    [BindProperty]
    public InModel InRequest { get; set; } = new();

    [BindProperty]
    public OutModel OutRequest { get; set; } = new();

    [BindProperty]
    public TransferModel TransferRequest { get; set; } = new();

    public IReadOnlyList<ProductDto> Products { get; set; } = [];
    public IReadOnlyList<WarehouseDto> Warehouses { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadCatalogsAsync();
    }

    public async Task<IActionResult> OnPostInAsync()
    {
        Tab = "in";
        var jwt = User.GetJwt();
        if (string.IsNullOrEmpty(jwt)) return RedirectToPage("/Account/Login");

        try
        {
            await _apiClient.RegisterInAsync(
                new StockInRequest(InRequest.ProductId, InRequest.WarehouseId, InRequest.Quantity, InRequest.UnitCost, InRequest.Note), jwt);
            SuccessMessage = "Entrada registrada correctamente.";
            InRequest = new InModel();
        }
        catch (StockDeskApiException ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadCatalogsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostOutAsync()
    {
        Tab = "out";
        var jwt = User.GetJwt();
        if (string.IsNullOrEmpty(jwt)) return RedirectToPage("/Account/Login");

        try
        {
            await _apiClient.RegisterOutAsync(
                new StockOutRequest(OutRequest.ProductId, OutRequest.WarehouseId, OutRequest.Quantity, OutRequest.Note), jwt);
            SuccessMessage = "Salida registrada correctamente.";
            OutRequest = new OutModel();
        }
        catch (StockDeskApiException ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadCatalogsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostTransferAsync()
    {
        Tab = "transfer";
        var jwt = User.GetJwt();
        if (string.IsNullOrEmpty(jwt)) return RedirectToPage("/Account/Login");

        try
        {
            await _apiClient.RegisterTransferAsync(
                new StockTransferRequest(
                    TransferRequest.ProductId,
                    TransferRequest.SourceWarehouseId,
                    TransferRequest.DestinationWarehouseId,
                    TransferRequest.Quantity,
                    TransferRequest.Note), jwt);
            SuccessMessage = "Transferencia registrada correctamente.";
            TransferRequest = new TransferModel();
        }
        catch (StockDeskApiException ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadCatalogsAsync();
        return Page();
    }

    private async Task LoadCatalogsAsync()
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
        }
        catch (StockDeskApiException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    public class InModel
    {
        [Required] public int ProductId { get; set; }
        [Required] public int WarehouseId { get; set; }
        [Range(0.0001, double.MaxValue)] public decimal Quantity { get; set; }
        [Range(0, double.MaxValue)] public decimal UnitCost { get; set; }
        public string? Note { get; set; }
    }

    public class OutModel
    {
        [Required] public int ProductId { get; set; }
        [Required] public int WarehouseId { get; set; }
        [Range(0.0001, double.MaxValue)] public decimal Quantity { get; set; }
        public string? Note { get; set; }
    }

    public class TransferModel
    {
        [Required] public int ProductId { get; set; }
        [Required] public int SourceWarehouseId { get; set; }
        [Required] public int DestinationWarehouseId { get; set; }
        [Range(0.0001, double.MaxValue)] public decimal Quantity { get; set; }
        public string? Note { get; set; }
    }
}
