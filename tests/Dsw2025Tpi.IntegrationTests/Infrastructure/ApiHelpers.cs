using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Dsw2025Tpi.IntegrationTests.Infrastructure;

public static class ApiHelpers
{
    public static async Task<string> LoginAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Username = username,
            Password = password
        });

        response.EnsureSuccessStatusCode();

        using var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (jsonDoc.RootElement.TryGetProperty("token", out var tokenProp))
        {
            return tokenProp.GetString() ?? string.Empty;
        }

        throw new InvalidOperationException("La respuesta de /api/auth/login no contiene 'token'.");
    }

    public static Task<string> GetAdminTokenAsync(HttpClient client)
    {
        return LoginAsync(client, "admin", "Admin123!");
    }

    public static void SetBearerToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public static async Task<HttpResponseMessage> RegisterUserAsync(
        HttpClient client,
        string username,
        string email,
        string password)
    {
        return await client.PostAsJsonAsync("/api/auth/register", new
        {
            Username = username,
            Email = email,
            Password = password
        });
    }

    public static async Task<Product> SeedProductAsync(
        CustomWebApplicationFactory factory,
        string sku,
        string name = "Producto Test",
        decimal price = 1500m,
        int stock = 10,
        bool isActive = true)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();

        var product = new Product
        {
            Sku = sku,
            InternalCode = $"INT-{sku}",
            Name = name,
            Description = "Producto generado para prueba de integración",
            CurrentUnitPrice = price,
            StockQuantity = stock,
            IsActive = isActive
        };

        context.Set<Product>().Add(product);
        await context.SaveChangesAsync();
        return product;
    }
}