using BookSpace.Application.Abstractions;
using BookSpace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookSpace.IntegrationTests;

public sealed class BookSpaceApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"bookspace-tests-{Guid.NewGuid():N}.db");
    private readonly Action<IServiceCollection>? _configureTestServices;
    private readonly IReadOnlyDictionary<string, string?>? _configuration;

    public BookSpaceApiFactory()
    {
    }

    internal BookSpaceApiFactory(Action<IServiceCollection> configureTestServices)
    {
        _configureTestServices = configureTestServices;
    }

    internal BookSpaceApiFactory(IReadOnlyDictionary<string, string?> configuration)
    {
        _configuration = configuration;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var testSettings = new Dictionary<string, string?>
            {
                ["RateLimiting:Authentication:Login:PermitLimit"] = "10000",
                ["RateLimiting:Authentication:Refresh:PermitLimit"] = "10000"
            };
            if (_configuration is not null)
            {
                foreach (var setting in _configuration)
                {
                    testSettings[setting.Key] = setting.Value;
                }
            }

            configurationBuilder.AddInMemoryCollection(testSettings);
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<BookSpaceDbContext>>();
            services.RemoveAll<BookSpaceDbContext>();
            services.RemoveAll<IBookSpaceDbContext>();
            services.AddDbContext<BookSpaceDbContext>(options =>
                options.UseSqlite($"Data Source={_databasePath}"));
            services.AddScoped<IBookSpaceDbContext>(provider =>
                provider.GetRequiredService<BookSpaceDbContext>());
            _configureTestServices?.Invoke(services);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        TryDelete(_databasePath);
        TryDelete($"{_databasePath}-wal");
        TryDelete($"{_databasePath}-shm");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // The OS can briefly retain a SQLite handle after the test host stops.
        }
        catch (UnauthorizedAccessException)
        {
            // The file is isolated in the system temp folder and can be reclaimed later.
        }
    }
}
