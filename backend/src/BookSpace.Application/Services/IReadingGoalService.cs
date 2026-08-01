using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public interface IReadingGoalService
{
    Task<PageResult<ReadingGoalDto>> GetGoalsAsync(
        Guid userId,
        ReadingGoalStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ReadingGoalDto> GetGoalAsync(
        Guid userId,
        Guid goalId,
        CancellationToken cancellationToken);

    Task<ReadingGoalDto> CreateAsync(
        Guid userId,
        CreateReadingGoalRequest request,
        CancellationToken cancellationToken);

    Task<ReadingGoalDto> UpdateAsync(
        Guid userId,
        Guid goalId,
        UpdateReadingGoalRequest request,
        CancellationToken cancellationToken);

    Task SynchronizeCompletionsAsync(Guid userId, CancellationToken cancellationToken);

    Task DeleteAsync(Guid userId, Guid goalId, CancellationToken cancellationToken);
}
