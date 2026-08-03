using BookSpace.Application.Abstractions;
using BookSpace.Application.Services;
using BookSpace.Infrastructure.External;
using BookSpace.Infrastructure.Persistence;
using BookSpace.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BookSpace.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBookSpaceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? "Data Source=data/bookspace.db";
        EnsureSqliteDirectory(connectionString);
        services.AddDbContext<BookSpaceDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IBookSpaceDbContext>(provider => provider.GetRequiredService<BookSpaceDbContext>());
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<BookstoreIntegrationOptions>(
            configuration.GetSection(BookstoreIntegrationOptions.SectionName));
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddHttpClient<IExternalBookProvider, ExternalBookProvider>((provider, client) =>
        {
            var options = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<BookstoreIntegrationOptions>>()
                .Value;
            client.BaseAddress = new Uri($"{options.BaseUrl.TrimEnd('/')}/");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 30));
        });
        services.AddScoped<DatabaseInitializer>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<IUserSafetyService, UserSafetyService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IReadingService, ReadingService>();
        services.AddScoped<ICommunityService, CommunityService>();
        services.AddScoped<IClubService, ClubService>();
        services.AddScoped<IClubChatService, ClubChatService>();
        services.AddScoped<IDirectMessageService, DirectMessageService>();
        services.AddScoped<IClubReadingSprintService, ClubReadingSprintService>();
        services.AddScoped<IChallengeService, ChallengeService>();
        services.AddScoped<IChallengeProgressSynchronizer, ChallengeProgressSynchronizer>();
        services.AddScoped<ChallengeMutationBoundary>();
        services.AddScoped<IChallengeMutationBoundary>(provider =>
            provider.GetRequiredService<ChallengeMutationBoundary>());
        services.AddScoped<IReadingMutationBoundary>(provider =>
            provider.GetRequiredService<ChallengeMutationBoundary>());
        services.AddScoped<IClubChatMutationBoundary>(provider =>
            provider.GetRequiredService<ChallengeMutationBoundary>());
        services.AddScoped<IDirectMessageMutationBoundary>(provider =>
            provider.GetRequiredService<ChallengeMutationBoundary>());
        services.AddScoped<IOnboardingMutationBoundary>(provider =>
            provider.GetRequiredService<ChallengeMutationBoundary>());
        services.AddScoped<IExternalCatalogMutationBoundary>(provider =>
            provider.GetRequiredService<ChallengeMutationBoundary>());
        services.AddScoped<IChallengeParticipationReader, ChallengeParticipationReader>();
        services.AddScoped<IChallengeProgressPersistence, ChallengeProgressPersistence>();
        services.AddScoped<IFollowMutationBoundary, FollowMutationBoundary>();
        services.AddScoped<IAsyncQueryExecutor, EfAsyncQueryExecutor>();
        services.AddScoped<IUserDiscoveryQuery, UserDiscoveryQuery>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IContentModerationService, ContentModerationService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReadingInsightsRepository, ReadingInsightsRepository>();
        services.AddScoped<IReadingInsightsService, ReadingInsightsService>();
        services.AddScoped<IExternalCatalogService, ExternalCatalogService>();
        services.AddSingleton(TimeProvider.System);
        services.AddReadingGoals();
        services.AddReadingNotes();
        return services;
    }

    private static void EnsureSqliteDirectory(string connectionString)
    {
        const string prefix = "Data Source=";
        var part = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (part is null)
        {
            return;
        }

        var path = part[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(path) || path == ":memory:")
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
