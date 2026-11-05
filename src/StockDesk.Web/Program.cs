using Microsoft.AspNetCore.Authentication.Cookies;
using StockDesk.Web.ApiClient;

var builder = WebApplication.CreateBuilder(args);

// ---- Cliente HTTP hacia la API (vía IHttpClientFactory) ----
var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5080";
builder.Services.AddHttpClient<IStockDeskApiClient, StockDeskApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

// ---- Autenticación basada en cookie ----
// El JWT emitido por la API se guarda como un claim dentro de la cookie de sesión
// del sitio Razor Pages. El servidor lo reenvía en el header Authorization al
// llamar a la API; el navegador nunca ve ni maneja el JWT directamente.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.Cookie.Name = "StockDesk.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireAuthenticatedUser", policy => policy.RequireAuthenticatedUser());

builder.Services.AddRazorPages(options =>
{
    // Todas las páginas requieren autenticación salvo las de la carpeta /Account
    // (login) y la página de error.
    options.Conventions.AuthorizeFolder("/", "RequireAuthenticatedUser");
    options.Conventions.AllowAnonymousToFolder("/Account");
    options.Conventions.AllowAnonymousToPage("/Error");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
