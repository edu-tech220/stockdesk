using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using StockDesk.Api.Data;
using StockDesk.Api.Dtos;
using StockDesk.Api.Tests.Infrastructure;

namespace StockDesk.Api.Tests;

public class AuthEndpointTests : IClassFixture<StockDeskWebApplicationFactory>
{
    private readonly StockDeskWebApplicationFactory _factory;

    public AuthEndpointTests(StockDeskWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_WithSeededAdminCredentials_ReturnsJwt()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(DbSeeder.DefaultAdminUsername, DbSeeder.DefaultAdminPassword));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal("Admin", body.Role);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(DbSeeder.DefaultAdminUsername, "WrongPassword!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OperatorRole_CannotCreateProduct_Returns403()
    {
        var client = _factory.CreateClient();

        // Crear usuario Operator vía login de admin.
        var adminToken = await LoginAsync(client, DbSeeder.DefaultAdminUsername, DbSeeder.DefaultAdminPassword);

        var createUserRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/users")
        {
            Content = JsonContent.Create(new CreateUserRequest("operator1", "Operator123!", "Operator"))
        };
        createUserRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var createUserResponse = await client.SendAsync(createUserRequest);
        createUserResponse.EnsureSuccessStatusCode();

        var operatorToken = await LoginAsync(client, "operator1", "Operator123!");

        var createProductRequest = new HttpRequestMessage(HttpMethod.Post, "/api/products")
        {
            Content = JsonContent.Create(new CreateProductRequest("SKU-FORBIDDEN", "Producto prohibido", null, "pieza", 5))
        };
        createProductRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        var response = await client.SendAsync(createProductRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminRole_CanCreateProduct_Returns201()
    {
        var client = _factory.CreateClient();
        var adminToken = await LoginAsync(client, DbSeeder.DefaultAdminUsername, DbSeeder.DefaultAdminPassword);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/products")
        {
            Content = JsonContent.Create(new CreateProductRequest("SKU-ADMIN-OK", "Producto admin", null, "pieza", 5))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<string> LoginAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }
}
