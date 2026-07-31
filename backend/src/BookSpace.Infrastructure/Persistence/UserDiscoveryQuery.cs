using BookSpace.Application.Abstractions;
using BookSpace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookSpace.Infrastructure.Persistence;

public sealed class UserDiscoveryQuery : IUserDiscoveryQuery
{
    public IQueryable<User> ApplyDisplayNameSearch(IQueryable<User> users, string search)
    {
        var escapedSearch = search
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
        var pattern = $"%{escapedSearch}%";

        return users.Where(user =>
            EF.Functions.Like(
                EF.Functions.Collate(user.DisplayName, "NOCASE"),
                pattern,
                @"\"));
    }
}
