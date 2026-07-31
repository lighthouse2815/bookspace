using BookSpace.Domain.Entities;

namespace BookSpace.Application.Abstractions;

public interface IUserDiscoveryQuery
{
    IQueryable<User> ApplyDisplayNameSearch(IQueryable<User> users, string search);
}
