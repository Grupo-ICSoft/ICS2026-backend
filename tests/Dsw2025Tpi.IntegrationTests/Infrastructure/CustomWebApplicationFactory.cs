using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Dsw2025Tpi.IntegrationTests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Dsw2025Tpi.Api.Program>, IAsyncLifetime
{
    private readonly string _tempDirectory;
    private readonly string _authDbPath;
    private readonly string _entitiesDbPath;

    public CustomWebApplicationFactory()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"ics2026-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
        _authDbPath = Path.Combine(_tempDirectory, "auth.db");
        _entitiesDbPath = Path.Combine(_tempDirectory, "entities.db");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var testSettings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:AuthenticateConnection"] = $"Data Source={_authDbPath}",
                ["ConnectionStrings:Dsw2025TpiEntities"] = $"Data Source={_entitiesDbPath}",
                ["Jwt:Key"] = "ClaveSuperSecretaDePruebaParaTestingConAlMenos32Caracteres!",
                ["Jwt:Issuer"] = "Dsw2025Tpi",
                ["Jwt:Audience"] = "Dsw2025TpiUsers"
            };
            config.AddInMemoryCollection(testSettings);
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}

[CollectionDefinition("IntegrationTests")]
public class IntegrationTestsCollection : ICollectionFixture<CustomWebApplicationFactory>
{
}