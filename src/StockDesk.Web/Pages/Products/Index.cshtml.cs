using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StockDesk.Web.ApiClient;
using StockDesk.Web.Models;

namespace StockDesk.Web.Pages.Products;

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

    public IReadOnlyList<ProductDto> Products { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public bool IsAdmin => User.IsAdmin();

    public async Task OnGetAsync()
    {
        await LoadProductsAsync();
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
            await LoadProductsAsync();
            return Page();
        }

        try
        {
            await _apiClient.CreateProductAsync(
                new CreateProductRequest(Input.Sku, Input.Name, Input.Description, Input.Unit, Input.ReorderPoint), jwt);
            SuccessMessage = $"Producto '{Input.Sku}' creado correctamente.";
            Input = new InputModel();
        }
        catch (StockDeskApiException ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadProductsAsync();
        return Page();
    }

    private async Task LoadProductsAsync()
    {
        var jwt = User.GetJwt();
        if (string.IsNullOrEmpty(jwt))
        {
            return;
        }

        try
        {
            Products = await _apiClient.GetProductsAsync(jwt);
        }
        catch (StockDeskApiException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    public class InputModel
    {
        [Required]
        public string Sku { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public string Unit { get; set; } = "pieza";

        [Range(0, int.MaxValue)]
        public int ReorderPoint { get; set; }
    }
}
