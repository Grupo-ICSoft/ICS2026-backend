using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dsw2025Tpi.IntegrationTests.Infrastructure;

namespace Dsw2025Tpi.IntegrationTests;

[Collection("IntegrationTests")]
public class SmokeTests
{
    private readonly HttpClient _client;

    public SmokeTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_AsSeededAdmin_Returns200WithToken()
    {
        // Arrange
        var credentials = new { username = "admin", password = "Admin123!" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", credentials);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        //Assert.True(response.StatusCode == HttpStatusCode.OK, $"Falló el login ({response.StatusCode}): {body}");
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"Demo de bloqueo ({response.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("token", out var tokenElement));
        Assert.False(string.IsNullOrWhiteSpace(tokenElement.GetString()));
    }
}