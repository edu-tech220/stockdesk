using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StockDesk.Web.ApiClient;

namespace StockDesk.Web.Pages.Account;

public class LoginModel : PageModel
{
    private readonly IStockDeskApiClient _apiClient;

    public LoginModel(IStockDeskApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var result = await _apiClient.LoginAsync(Input.Username, Input.Password);
            if (result is null)
            {
                ErrorMessage = "Usuario o contraseña incorrectos.";
                return Page();
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, result.Username),
                new(ClaimTypes.Role, result.Role),
                new(UserClaimsExtensions.JwtClaimType, result.Token)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToPage("/Stock/Index");
        }
        catch (StockDeskApiException ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "No fue posible conectar con la API de StockDesk. Verifique que el backend esté disponible.";
            return Page();
        }
    }

    public class InputModel
    {
        [Required(ErrorMessage = "El usuario es obligatorio.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        public string Password { get; set; } = string.Empty;
    }
}
