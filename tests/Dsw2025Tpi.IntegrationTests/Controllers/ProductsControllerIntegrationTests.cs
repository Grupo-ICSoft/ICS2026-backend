using System.Net;
using System.Net.Http.Json;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dsw2025Tpi.IntegrationTests.Controllers;

[Collection("IntegrationTests")]
public class ProductsControllerIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ProductsControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AddProduct_WhenUnauthenticatedOrNotAdmin_ReturnsUnauthorizedOrForbidden()
    {
        // Arrange
        var sku = $"TEST-{Guid.NewGuid():N}";

        var productRequest = new
        {
            Sku = sku,
            InternalCode = $"INT-{sku}",
            Name = "Producto de prueba",
            Description = "Producto para prueba de autorización",
            CurrentUnitPrice = 1500m,
            StockQuantity = 10
        };

        // Act - Solicitud sin autenticación
        using var anonymousClient = _factory.CreateClient();

        var anonymousResponse = await anonymousClient.PostAsJsonAsync(
            "/api/products",
            productRequest);

        // Assert - Sin token debe devolver 401
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        // Arrange - Registrar un usuario común
        using var userClient = _factory.CreateClient();

        var suffix = Guid.NewGuid().ToString("N");
        var username = $"user{suffix}";
        var email = $"user{suffix}@example.com";
        var password = "User123!";

        var registerResponse = await ApiHelpers.RegisterUserAsync(
            userClient,
            username,
            email,
            password);

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var userToken = await ApiHelpers.LoginAsync(
            userClient,
            username,
            password);

        ApiHelpers.SetBearerToken(userClient, userToken);

        // Act - Solicitud autenticada como USER
        var userResponse = await userClient.PostAsJsonAsync(
            "/api/products",
            productRequest);

        // Assert - Un usuario común debe recibir 403
        Assert.Equal(HttpStatusCode.Forbidden, userResponse.StatusCode);
    }

    [Fact]
    public async Task AddProduct_WhenAuthenticatedAsAdmin_PersistsInSqliteAndReturnsCreated()
    {
        // Arrange
        using var client = _factory.CreateClient();

        var adminToken = await ApiHelpers.GetAdminTokenAsync(client);
        ApiHelpers.SetBearerToken(client, adminToken);

        var sku = $"TEST-{Guid.NewGuid():N}";

        var productRequest = new
        {
            Sku = sku,
            InternalCode = $"INT-{sku}",
            Name = "Producto creado por Admin",
            Description = "Prueba de persistencia en SQLite",
            CurrentUnitPrice = 2500m,
            StockQuantity = 20
        };

        // Act - Crear producto mediante HTTP
        var response = await client.PostAsJsonAsync(
            "/api/products",
            productRequest);

        // Assert - Debe devolver 201 Created
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Assert - Verificar persistencia real en SQLite
        using var scope = _factory.Services.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<Dsw2025TpiContext>();

        var products = await context.Set<Product>()
            .AsNoTracking()
            .Where(p => p.Sku == sku)
            .ToListAsync();

        var persistedProduct = Assert.Single(products);

        Assert.Equal(productRequest.Sku, persistedProduct.Sku);
        Assert.Equal(productRequest.InternalCode, persistedProduct.InternalCode);
        Assert.Equal(productRequest.Name, persistedProduct.Name);
        Assert.Equal(productRequest.Description, persistedProduct.Description);
        Assert.Equal(productRequest.CurrentUnitPrice, persistedProduct.CurrentUnitPrice);
        Assert.Equal(productRequest.StockQuantity, persistedProduct.StockQuantity);
    }

    [Fact]
    public async Task AddProduct_WhenSkuAlreadyExistsInDatabase_ReturnsConflictStatusCode()
    {
        // Arrange - Preparar un producto existente en SQLite
        var sku = $"DUP-{Guid.NewGuid():N}";

        await ApiHelpers.SeedProductAsync(
            _factory,
            sku,
            "Producto original",
            1500m,
            10);

        using var scope = _factory.Services.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<Dsw2025TpiContext>();

        var originalProducts = await context.Set<Product>()
            .AsNoTracking()
            .Where(p => p.Sku == sku)
            .ToListAsync();

        var originalProduct = Assert.Single(originalProducts);

        // Arrange - Preparar solicitud con el mismo SKU
        using var client = _factory.CreateClient();

        var adminToken = await ApiHelpers.GetAdminTokenAsync(client);
        ApiHelpers.SetBearerToken(client, adminToken);

        var productRequest = new
        {
            Sku = sku,
            InternalCode = $"NEW-{sku}",
            Name = "Producto duplicado",
            Description = "Este producto no debe guardarse",
            CurrentUnitPrice = 3000m,
            StockQuantity = 30
        };

        // Act - Intentar crear el producto duplicado
        var response = await client.PostAsJsonAsync(
            "/api/products",
            productRequest);

        // Assert - El middleware devuelve 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Assert - Verificar que sigue existiendo una sola fila
        var productsAfterRequest = await context.Set<Product>()
            .AsNoTracking()
            .Where(p => p.Sku == sku)
            .ToListAsync();

        var productAfterRequest = Assert.Single(productsAfterRequest);

        Assert.Equal(originalProduct.Name, productAfterRequest.Name);
        Assert.Equal(originalProduct.InternalCode, productAfterRequest.InternalCode);
        Assert.Equal(originalProduct.CurrentUnitPrice, productAfterRequest.CurrentUnitPrice);
        Assert.Equal(originalProduct.StockQuantity, productAfterRequest.StockQuantity);
    }
}
