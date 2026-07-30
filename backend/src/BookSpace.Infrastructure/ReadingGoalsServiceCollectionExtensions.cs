using BookSpace.Application.Abstractions;
using BookSpace.Application.Services;
using BookSpace.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BookSpace.Infrastructure;

public static class ReadingGoalsServiceCollectionExtensions
{
    public static IServiceCollection AddReadingGoals(this IServiceCollection services)
    {
        services.AddScoped<IReadingGoalRepository, ReadingGoalRepository>();
        services.AddScoped<IReadingGoalService, ReadingGoalService>();
        return services;
    }
}
