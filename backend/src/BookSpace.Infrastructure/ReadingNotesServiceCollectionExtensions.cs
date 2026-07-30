using BookSpace.Application.Abstractions;
using BookSpace.Application.Services;
using BookSpace.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BookSpace.Infrastructure;

public static class ReadingNotesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Reading Notes slice. The application's composition root
    /// can opt into it without modifying the feature's implementation files.
    /// </summary>
    public static IServiceCollection AddReadingNotes(this IServiceCollection services)
    {
        services.AddScoped<IReadingNoteRepository, ReadingNoteRepository>();
        services.AddScoped<IReadingNoteService, ReadingNoteService>();
        return services;
    }
}
