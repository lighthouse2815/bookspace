using System.Security.Claims;
using BookSpace.Api.Common;
using BookSpace.Infrastructure;
using BookSpace.Infrastructure.Observability;
using BookSpace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookSpace.IntegrationTests;

public sealed class ObservabilityTests
{
    [Fact]
    public async Task Database_health_check_reports_healthy_for_the_configured_BookSpace_database()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"bookspace-health-{Guid.NewGuid():N}.db");

        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<BookSpaceDbContext>(options =>
                options.UseSqlite($"Data Source={databasePath}"));
            services.AddBookSpaceDatabaseHealthCheck();

            await using var provider = services.BuildServiceProvider();
            await using (var scope = provider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<BookSpaceDbContext>();
                await dbContext.Database.MigrateAsync();
            }

            var healthChecks = provider.GetRequiredService<HealthCheckService>();

            var report = await healthChecks.CheckHealthAsync(
                registration => registration.Tags.Contains("ready"));

            Assert.Equal(HealthStatus.Healthy, report.Status);
            var database = Assert.Single(report.Entries);
            Assert.Equal("bookspace_database", database.Key);
            Assert.Equal(HealthStatus.Healthy, database.Value.Status);

            var registration = Assert.Single(provider
                .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
                .Value
                .Registrations);
            Assert.Equal(
                TimeSpan.FromSeconds(
                    BookSpace.Infrastructure.HealthCheckServiceCollectionExtensions
                        .DatabaseTimeoutSeconds),
                registration.Timeout);
        }
        finally
        {
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task Database_health_check_returns_a_controlled_failure_when_the_context_is_unavailable()
    {
        var options = new DbContextOptionsBuilder<BookSpaceDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var dbContext = new BookSpaceDbContext(options);
        await dbContext.DisposeAsync();
        var healthCheck = new BookSpaceDatabaseHealthCheck(dbContext);

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Không thể kết nối cơ sở dữ liệu BookSpace.", result.Description);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void Trusted_forwarding_accepts_only_configured_proxy_addresses_and_networks()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:ForwardLimit"] = "2",
                ["ForwardedHeaders:KnownProxies:0"] = "10.0.0.10",
                ["ForwardedHeaders:KnownNetworks:0"] = "10.20.0.0/16"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddBookSpaceTrustedForwarding(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.Equal(2, options.ForwardLimit);
        Assert.True(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor));
        Assert.True(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedProto));
        Assert.Contains(System.Net.IPAddress.Parse("10.0.0.10"), options.KnownProxies);
        Assert.Contains(System.Net.IPNetwork.Parse("10.20.0.0/16"), options.KnownIPNetworks);
    }

    [Fact]
    public async Task Health_response_hides_database_failure_details()
    {
        const string sensitiveFailure = "Data Source=C:\\secrets\\bookspace.db;Password=do-not-return";
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["bookspace_database"] = new(
                    HealthStatus.Unhealthy,
                    sensitiveFailure,
                    TimeSpan.FromMilliseconds(4),
                    new InvalidOperationException(sensitiveFailure),
                    new Dictionary<string, object>())
            },
            TimeSpan.FromMilliseconds(4));
        var context = NewHttpContext();
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        await BookSpaceHealthEndpoint.WriteResponseAsync(context, report);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Equal("Unhealthy", body);
        Assert.DoesNotContain(sensitiveFailure, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Request_observability_reuses_a_valid_correlation_id_and_logs_safe_structured_fields()
    {
        var logger = new CapturingLogger<RequestObservabilityMiddleware>();
        var userId = Guid.NewGuid();
        var middleware = new RequestObservabilityMiddleware(
            context =>
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                    "test"));
                context.Response.StatusCode = StatusCodes.Status202Accepted;
                return Task.CompletedTask;
            },
            logger);
        var context = NewHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = $"/api/books/{Guid.NewGuid()}";
        context.Request.QueryString = new QueryString("?access_token=secret-token");
        context.Request.Headers[RequestObservabilityMiddleware.CorrelationIdHeaderName] =
            "client-request_123";
        context.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/api/books/{id:guid}"),
            order: 0,
            EndpointMetadataCollection.Empty,
            displayName: "book-by-id"));

        await middleware.InvokeAsync(context);

        Assert.Equal("client-request_123", context.TraceIdentifier);
        Assert.Equal(
            "client-request_123",
            context.Response.Headers[RequestObservabilityMiddleware.CorrelationIdHeaderName]);

        Assert.NotNull(logger.LastScope);
        var scope = logger.LastScope;
        Assert.Equal("client-request_123", scope["CorrelationId"]);
        Assert.Equal(HttpMethods.Post, scope["RequestMethod"]);
        Assert.Equal("/api/books/{id:guid}", scope["Route"]);
        Assert.Equal(context.Request.Path.Value, scope["RequestPath"]);
        Assert.Equal(StatusCodes.Status202Accepted, scope["StatusCode"]);
        Assert.Equal(userId.ToString(), scope["UserId"]);
        Assert.True(Assert.IsType<double>(scope["ElapsedMilliseconds"]) >= 0);
        Assert.DoesNotContain(
            scope.Values,
            value => value?.ToString()?.Contains("secret-token", StringComparison.Ordinal) == true);

        Assert.NotNull(logger.LastLog);
        var log = logger.LastLog;
        Assert.Equal("client-request_123", log["CorrelationId"]);
        Assert.Equal(userId.ToString(), log["UserId"]);
        Assert.Equal(HttpMethods.Post, log["RequestMethod"]);
        Assert.Equal("/api/books/{id:guid}", log["Route"]);
        Assert.Equal(StatusCodes.Status202Accepted, log["StatusCode"]);
        Assert.DoesNotContain(
            log.Values,
            value => value?.ToString()?.Contains("secret-token", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Request_observability_replaces_an_invalid_correlation_id()
    {
        var logger = new CapturingLogger<RequestObservabilityMiddleware>();
        var middleware = new RequestObservabilityMiddleware(
            _ => Task.CompletedTask,
            logger);
        var context = NewHttpContext();
        context.Request.Headers[RequestObservabilityMiddleware.CorrelationIdHeaderName] =
            "correlation:id:with:colons";

        await middleware.InvokeAsync(context);

        var generated = context.Response
            .Headers[RequestObservabilityMiddleware.CorrelationIdHeaderName]
            .ToString();
        Assert.True(Guid.TryParseExact(generated, "N", out _));
        Assert.Equal(generated, context.TraceIdentifier);
    }

    private static DefaultHttpContext NewHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
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
            // SQLite can briefly retain a handle after the service provider is disposed.
        }
        catch (UnauthorizedAccessException)
        {
            // The file lives in the isolated system temp directory and can be reclaimed later.
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private object? _currentScope;

        public IReadOnlyDictionary<string, object?>? LastScope { get; private set; }
        public IReadOnlyDictionary<string, object?>? LastLog { get; private set; }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            var previous = _currentScope;
            _currentScope = state;
            return new CallbackDisposable(() => _currentScope = previous);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (_currentScope is IEnumerable<KeyValuePair<string, object?>> values)
            {
                LastScope = values.ToDictionary(item => item.Key, item => item.Value);
            }

            if (state is IEnumerable<KeyValuePair<string, object?>> logValues)
            {
                LastLog = logValues.ToDictionary(item => item.Key, item => item.Value);
            }
        }
    }

    private sealed class CallbackDisposable(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
