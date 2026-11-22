using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dsw2025Tpi.IntegrationTests.Controllers;

[Collection("IntegrationTests")]
public class OrdersControllerIntegrationTests
{
    private static readonly Guid SeededCustomerId = Guid.Parse("c7c6463e-bc3f-4bba-a15a-9e74f7a3cdee");

    private readonly CustomWebApplicationFactory _factory;

    public OrdersControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateOrder_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var product = await ApiHelpers.SeedProductAsync(_factory, $"ORD-{Guid.NewGuid():N}", stock: 10);
        using var anonymousClient = _factory.CreateClient();

        var response = await anonymousClient.PostAsJsonAsync(
            "/api/orders",
            BuildOrderRequest(product.Id, quantity: 1));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_WhenAuthenticatedWithValidData_ReturnsCreatedAndPersistsOrderAndStockInSqlite()
    {
        using var client = _factory.CreateClient();
        ApiHelpers.SetBearerToken(client, await ApiHelpers.GetAdminTokenAsync(client));

        var product = await ApiHelpers.SeedProductAsync(
            _factory,
            $"ORD-{Guid.NewGuid():N}",
            price: 250m,
            stock: 10);

        var response = await client.PostAsJsonAsync(
            "/api/orders",
            BuildOrderRequest(product.Id, quantity: 3));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = body.GetProperty("orderId").GetGuid();
        Assert.Equal(750m, body.GetProperty("totalAmount").GetDecimal());

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();

        var orderExists = await context.Set<Order>()
            .AsNoTracking()
            .AnyAsync(o => o.Id == orderId);

        var persistedProduct = await context.Set<Product>()
            .AsNoTracking()
            .SingleAsync(p => p.Id == product.Id);

        Assert.True(orderExists);
        Assert.Equal(7, persistedProduct.StockQuantity);
    }

    [Fact]
    public async Task CreateOrder_WhenStockIsInsufficient_ReturnsBadRequestWithErrorMessage()
    {
        using var client = _factory.CreateClient();
        ApiHelpers.SetBearerToken(client, await ApiHelpers.GetAdminTokenAsync(client));

        var product = await ApiHelpers.SeedProductAsync(_factory, $"ORD-{Guid.NewGuid():N}", stock: 2);

        var response = await client.PostAsJsonAsync(
            "/api/orders",
            BuildOrderRequest(product.Id, quantity: 5));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        var message = error.GetProperty("Message").GetString();
        Assert.Contains("stock", message, StringComparison.OrdinalIgnoreCase);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();

        var persistedProduct = await context.Set<Product>()
            .AsNoTracking()
            .SingleAsync(p => p.Id == product.Id);

        Assert.Equal(2, persistedProduct.StockQuantity);
    }

    private static object BuildOrderRequest(Guid productId, int quantity) => new
    {
        CustomerId = SeededCustomerId,
        ShippingAddress = "Calle Falsa 123",
        BillingAddress = "Calle Falsa 123",
        Notes = "Orden de prueba de integración",
        Items = new[] { new { ProductId = productId, Quantity = quantity } }
    };
}
