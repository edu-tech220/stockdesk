using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StockDesk.Api.Data;
using StockDesk.Api.Dtos;
using StockDesk.Api.Services;
using StockDesk.Api.Tests.Infrastructure;

namespace StockDesk.Api.Tests;

public class MovementsEndpointTests : IClassFixture<StockDeskWebApplicationFactory>
{
    private readonly StockDeskWebApplicationFactory _factory;

    public MovementsEndpointTests(StockDeskWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMovements_Kardex_ReturnsPagedResultsOrderedByMostRecent()
    {
        var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(DbSeeder.DefaultAdminUsername, DbSeeder.DefaultAdminPassword));
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        int productId;
        int warehouseId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StockDeskDbContext>();
            var product = TestDataHelper.AddProduct(db, "KARDEX-SKU");
            var warehouse = TestDataHelper.AddWarehouse(db, "KARDEX-WH");
            productId = product.Id;
            warehouseId = warehouse.Id;

            var service = new MovementService(db);
            for (var i = 0; i < 5; i++)
            {
                await service.RegisterInAsync(new StockInRequest(productId, warehouseId, 1m, 10m, $"Movimiento {i}"), userId: 1);
            }
        }

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/movements?productId={productId}&page=1&pageSize=2");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var page1 = await response.Content.ReadFromJsonAsync<PagedResult<MovementDto>>();

        Assert.NotNull(page1);
        Assert.Equal(5, page1!.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(3, page1.TotalPages);

        // Debe venir ordenado del más reciente al más antiguo ("Movimiento 4" primero).
        Assert.Equal("Movimiento 4", page1.Items[0].Note);
        Assert.Equal("Movimiento 3", page1.Items[1].Note);
    }
}
