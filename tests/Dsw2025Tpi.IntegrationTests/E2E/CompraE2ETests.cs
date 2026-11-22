using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dsw2025Tpi.IntegrationTests.Infrastructure;

namespace Dsw2025Tpi.IntegrationTests.E2E;

/// <summary>
/// TP4-10 - Prueba E2E de compra a nivel API.
/// Encadena peticiones HTTP reales contra la API levantada con SQLite (CustomWebApplicationFactory),
/// emulando lo que haría un usuario: el admin publica un producto, un cliente lo encuentra en el
/// catálogo, se registra, compra y consulta su orden.
/// Regla de la prueba: NO se accede al DbContext; todo el estado se verifica solo por HTTP.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "E2E")]
public class CompraE2ETests
{
    private readonly CustomWebApplicationFactory _factory;

    public CompraE2ETests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Compra_FlujoCompleto_CreaOrdenPendienteConTotalCorrectoYDescuentaStock()
    {
        // ---------- ARRANGE ----------
        // Datos únicos para no chocar con otros tests que comparten la misma base SQLite
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var sku = $"E2E-{suffix}";
        const decimal unitPrice = 1500m;
        const int initialStock = 10;
        const int purchasedQuantity = 3;

        // Un cliente HTTP por "actor" para que los tokens no se mezclen
        using var adminClient = _factory.CreateClient();
        using var anonymousClient = _factory.CreateClient();
        using var customerClient = _factory.CreateClient();

        // ---------- PASO 1: login del admin ----------
        var adminToken = await ApiHelpers.GetAdminTokenAsync(adminClient);
        Assert.False(string.IsNullOrWhiteSpace(adminToken));
        ApiHelpers.SetBearerToken(adminClient, adminToken);

        // ---------- PASO 2: el admin crea el producto ----------
        var createProductResponse = await adminClient.PostAsJsonAsync("/api/products", new
        {
            Sku = sku,
            InternalCode = $"INT-{sku}",
            Name = $"Mouse E2E {suffix}",
            Description = "Producto creado por la prueba E2E de compra",
            CurrentUnitPrice = unitPrice,
            StockQuantity = initialStock
        });

        Assert.Equal(HttpStatusCode.Created, createProductResponse.StatusCode);
        var createdProduct = await ReadJsonAsync(createProductResponse);
        var productId = createdProduct.GetProperty("id").GetGuid();

        // ---------- PASO 3: buscarlo en el catálogo público (sin token) ----------
        var catalogResponse = await anonymousClient.GetAsync(
            $"/api/products?search={sku}&pageNumber=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, catalogResponse.StatusCode);
        var catalog = await ReadJsonAsync(catalogResponse);
        var catalogItem = Assert.Single(
            catalog.GetProperty("productItems").EnumerateArray(),
            p => p.GetProperty("sku").GetString() == sku);
        Assert.Equal(productId, catalogItem.GetProperty("id").GetGuid());
        Assert.Equal(initialStock, catalogItem.GetProperty("stockQuantity").GetInt32());

        // ---------- PASO 4: registrar un cliente y loguearlo ----------
        var username = $"cliente{suffix}";
        var email = $"cliente{suffix}@example.com";
        const string password = "Cliente123!";

        var registerResponse = await ApiHelpers.RegisterUserAsync(customerClient, username, email, password);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var registerBody = await ReadJsonAsync(registerResponse);
        var customerId = registerBody.GetProperty("customerId").GetGuid();

        var customerToken = await ApiHelpers.LoginAsync(customerClient, username, password);
        ApiHelpers.SetBearerToken(customerClient, customerToken);

        // ---------- PASO 5 (ACT): el cliente compra ----------
        var createOrderResponse = await customerClient.PostAsJsonAsync("/api/orders", new
        {
            CustomerId = customerId,
            ShippingAddress = "Av. Mate de Luna 1234, San Miguel de Tucumán",
            BillingAddress = "Av. Mate de Luna 1234, San Miguel de Tucumán",
            Notes = "Orden generada por la prueba E2E",
            Items = new[]
            {
                new { ProductId = productId, Quantity = purchasedQuantity }
            }
        });

        // ---------- ASSERT ----------
        Assert.Equal(HttpStatusCode.Created, createOrderResponse.StatusCode);
        Assert.NotNull(createOrderResponse.Headers.Location);

        // PASO 6: consultar la orden por el header Location
        var getOrderResponse = await customerClient.GetAsync(createOrderResponse.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, getOrderResponse.StatusCode);
        var order = await ReadJsonAsync(getOrderResponse);

        Assert.Equal(unitPrice * purchasedQuantity, order.GetProperty("totalAmount").GetDecimal());
        Assert.Equal("PENDING", order.GetProperty("status").GetString());

        var orderItem = Assert.Single(order.GetProperty("items").EnumerateArray());
        Assert.Equal(productId, orderItem.GetProperty("productId").GetGuid());
        Assert.Equal(purchasedQuantity, orderItem.GetProperty("quantity").GetInt32());

        // PASO 7: verificar que el stock del producto se descontó
        var getProductResponse = await anonymousClient.GetAsync($"/api/products/{productId}");
        Assert.Equal(HttpStatusCode.OK, getProductResponse.StatusCode);
        var finalProduct = await ReadJsonAsync(getProductResponse);

        Assert.Equal(initialStock - purchasedQuantity, finalProduct.GetProperty("stockQuantity").GetInt32());
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }
}
