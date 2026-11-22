using System.Text.Json;
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

    [Fact]
    public async Task GetProducts_PublicEndpoint_ReturnsOkWithPaginatedJsonStructure()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N");
        var sku = $"TP407-PUBLIC-{suffix}";

        await ApiHelpers.SeedProductAsync(
            _factory,
            sku,
            $"Producto público {suffix}");

        using var client = _factory.CreateClient();

        // Act - Consulta pública, sin token
        var response = await client.GetAsync(
            $"/api/products?search={Uri.EscapeDataString(sku)}&pageNumber=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = document.RootElement;
        var productItems = root.GetProperty("productItems");

        Assert.Equal(JsonValueKind.Array, productItems.ValueKind);
        Assert.True(root.GetProperty("total").GetInt32() >= 1);

        Assert.Contains(
            productItems.EnumerateArray(),
            item => item.GetProperty("sku").GetString() == sku);
    }

    [Fact]
    public async Task GetProducts_WithQueryStringFilters_ReturnsFilteredResultsFromSqlite()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N");

        var firstSku = $"TP407-MOUSE-A-{suffix}";
        var secondSku = $"TP407-MOUSE-B-{suffix}";
        var keyboardSku = $"TP407-KEYBOARD-{suffix}";

        await ApiHelpers.SeedProductAsync(
            _factory, firstSku, $"Mouse-{suffix}-A");

        await ApiHelpers.SeedProductAsync(
            _factory, secondSku, $"mouse-{suffix}-B");

        await ApiHelpers.SeedProductAsync(
            _factory, keyboardSku, $"Teclado-{suffix}");

        using var client = _factory.CreateClient();

        // Act - Buscar Mouse sin distinguir mayúsculas y minúsculas
        var searchTerm = $"MOUSE-{suffix}";

        var response = await client.GetAsync(
            $"/api/products?search={Uri.EscapeDataString(searchTerm)}&pageNumber=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = document.RootElement;
        var productItems = root.GetProperty("productItems");

        Assert.Equal(JsonValueKind.Array, productItems.ValueKind);
        Assert.Equal(2, root.GetProperty("total").GetInt32());
        Assert.Equal(2, productItems.GetArrayLength());

        Assert.Contains(
            productItems.EnumerateArray(),
            item => item.GetProperty("sku").GetString() == firstSku);

        Assert.Contains(
            productItems.EnumerateArray(),
            item => item.GetProperty("sku").GetString() == secondSku);

        Assert.DoesNotContain(
            productItems.EnumerateArray(),
            item => item.GetProperty("sku").GetString() == keyboardSku);
    }

    [Fact]
    public async Task GetAuthProducts_AdminEndpoint_WhenCalledByAdmin_IncludesInactiveProducts()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N");

        var activeSku = $"TP407-ADMIN-ACTIVE-{suffix}";
        var inactiveSku = $"TP407-ADMIN-INACTIVE-{suffix}";
        var searchTerm = $"TP407-ADMIN-{suffix}";

        await ApiHelpers.SeedProductAsync(
            _factory,
            activeSku,
            $"TP407-ADMIN-{suffix}-Activo",
            isActive: true);

        await ApiHelpers.SeedProductAsync(
            _factory,
            inactiveSku,
            $"TP407-ADMIN-{suffix}-Inactivo",
            isActive: false);

        // Act - Consulta administrativa con token de Admin
        using var adminClient = _factory.CreateClient();

        var adminToken = await ApiHelpers.GetAdminTokenAsync(adminClient);
        ApiHelpers.SetBearerToken(adminClient, adminToken);

        var adminResponse = await adminClient.GetAsync(
            $"/api/products/admin?search={Uri.EscapeDataString(searchTerm)}&pageNumber=1&pageSize=10");

        // Assert - El administrador ve ambos productos
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);

        using var adminDocument = JsonDocument.Parse(
            await adminResponse.Content.ReadAsStringAsync());

        var adminItems = adminDocument.RootElement.GetProperty("productItems");

        Assert.Contains(
            adminItems.EnumerateArray(),
            item => item.GetProperty("sku").GetString() == activeSku);

        Assert.Contains(
            adminItems.EnumerateArray(),
            item => item.GetProperty("sku").GetString() == inactiveSku);

        // Act - Consulta pública sin token
        using var publicClient = _factory.CreateClient();

        var publicResponse = await publicClient.GetAsync(
            $"/api/products?search={Uri.EscapeDataString(searchTerm)}&pageNumber=1&pageSize=10");

        // Assert - El público ve el activo, pero no el inactivo
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);

        using var publicDocument = JsonDocument.Parse(
            await publicResponse.Content.ReadAsStringAsync());

        var publicItems = publicDocument.RootElement.GetProperty("productItems");

        Assert.Contains(
            publicItems.EnumerateArray(),
            item => item.GetProperty("sku").GetString() == activeSku);

        Assert.DoesNotContain(
            publicItems.EnumerateArray(),
            item => item.GetProperty("sku").GetString() == inactiveSku);
    }
}
