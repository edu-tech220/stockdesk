using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace StockDesk.Web.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        return User.Identity?.IsAuthenticated == true
            ? RedirectToPage("/Stock/Index")
            : RedirectToPage("/Account/Login");
    }
}
