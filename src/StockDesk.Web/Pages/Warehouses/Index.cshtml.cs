using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StockDesk.Web.ApiClient;
using StockDesk.Web.Models;

namespace StockDesk.Web.Pages.Warehouses;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IStockDeskApiClient _apiClient;

    public IndexModel(IStockDeskApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IReadOnlyList<WarehouseDto> Warehouses { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public bool IsAdmin => User.IsAdmin();

    public async Task OnGetAsync()
    {
        await LoadWarehousesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var jwt = User.GetJwt();
        if (string.IsNullOrEmpty(jwt))
        {
            return RedirectToPage("/Account/Login");
        }

        if (!ModelState.IsValid)
        {
            await LoadWarehousesAsync();
            return Page();
        }

        try
        {
            await _apiClient.CreateWarehouseAsync(new CreateWarehouseRequest(Input.Code, Input.Name, Input.Location), jwt);
            SuccessMessage = $"Almacén '{Input.Code}' creado correctamente.";
            Input = new InputModel();
        }
        catch (StockDeskApiException ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadWarehousesAsync();
        return Page();
    }

    private async Task LoadWarehousesAsync()
    {
        var jwt = User.GetJwt();
        if (string.IsNullOrEmpty(jwt))
        {
            return;
        }

        try
        {
            Warehouses = await _apiClient.GetWarehousesAsync(jwt);
        }
        catch (StockDeskApiException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    public class InputModel
    {
        [Required]
        public string Code { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Location { get; set; }
    }
}
