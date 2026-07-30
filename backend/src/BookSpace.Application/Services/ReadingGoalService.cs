using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Services;

public sealed class ReadingGoalService(IReadingGoalRepository repository) : IReadingGoalService
{
    public async Task<PageResult<ReadingGoalDto>> GetGoalsAsync(
        Guid userId,
        ReadingGoalStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await SynchronizeCompletionsAsync(userId, now, cancellationToken);
        var (normalizedPage, size, skip) = Paging.Normalize(page, pageSize);
        var result = await repository.SearchAsync(
            new ReadingGoalSearchCriteria(userId, status, skip, size, now),
            cancellationToken);

        var items = new List<ReadingGoalDto>(result.Items.Count);
        var changed = false;
        foreach (var goal in result.Items)
        {
            var mapped = await MapAsync(goal, now, cancellationToken);
            items.Add(mapped.Dto);
            changed |= mapped.Changed;
        }

        if (changed)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }

        return PageResult<ReadingGoalDto>.Create(items, normalizedPage, size, result.TotalItems);
    }

    private async Task SynchronizeCompletionsAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var pendingGoals = await repository.GetPendingOwnedAsync(userId, cancellationToken);
        var changed = false;
        foreach (var goal in pendingGoals)
        {
            changed |= (await MapAsync(goal, now, cancellationToken)).Changed;
        }

        if (changed)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<ReadingGoalDto> GetGoalAsync(
        Guid userId,
        Guid goalId,
        CancellationToken cancellationToken)
    {
        var goal = await FindOwnedAsync(userId, goalId, cancellationToken);
        var mapped = await MapAsync(goal, DateTimeOffset.UtcNow, cancellationToken);
        if (mapped.Changed)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }

        return mapped.Dto;
    }

    public async Task<ReadingGoalDto> CreateAsync(
        Guid userId,
        CreateReadingGoalRequest request,
        CancellationToken cancellationToken)
    {
        ValidateGoalDefinition(request.Metric, request.Period);
        ValidateWritableDate(request.EndDate);
        await EnsureNoOverlapAsync(
            userId,
            request.Metric,
            request.StartDate,
            request.EndDate,
            null,
            cancellationToken);

        var goal = new ReadingGoal(
            userId,
            request.Metric,
            request.Period,
            request.TargetValue,
            request.StartDate,
            request.EndDate);
        repository.Add(goal);

        var mapped = await MapAsync(goal, DateTimeOffset.UtcNow, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return mapped.Dto;
    }

    public async Task<ReadingGoalDto> UpdateAsync(
        Guid userId,
        Guid goalId,
        UpdateReadingGoalRequest request,
        CancellationToken cancellationToken)
    {
        var goal = await FindOwnedAsync(userId, goalId, cancellationToken);
        ValidateGoalDefinition(request.Metric, request.Period);
        ValidateWritableDate(request.EndDate);
        await EnsureNoOverlapAsync(
            userId,
            request.Metric,
            request.StartDate,
            request.EndDate,
            goalId,
            cancellationToken);

        goal.Update(
            request.Metric,
            request.Period,
            request.TargetValue,
            request.StartDate,
            request.EndDate);

        var mapped = await MapAsync(goal, DateTimeOffset.UtcNow, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return mapped.Dto;
    }

    public async Task DeleteAsync(Guid userId, Guid goalId, CancellationToken cancellationToken)
    {
        var goal = await FindOwnedAsync(userId, goalId, cancellationToken);
        goal.SoftDelete();
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<ReadingGoal> FindOwnedAsync(
        Guid userId,
        Guid goalId,
        CancellationToken cancellationToken) =>
        await repository.GetOwnedAsync(userId, goalId, cancellationToken)
        ?? throw ServiceErrors.NotFound(
            "READING_GOAL_NOT_FOUND",
            "Không tìm thấy mục tiêu đọc.");

    private async Task EnsureNoOverlapAsync(
        Guid userId,
        ReadingGoalMetric metric,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        Guid? excludedGoalId,
        CancellationToken cancellationToken)
    {
        if (await repository.HasOverlappingActiveGoalAsync(
                userId,
                metric,
                startDate,
                endDate,
                excludedGoalId,
                cancellationToken))
        {
            throw ServiceErrors.Conflict(
                "READING_GOAL_OVERLAPS",
                "Bạn đã có mục tiêu đang hoạt động cùng loại trong khoảng thời gian này.");
        }
    }

    private async Task<(ReadingGoalDto Dto, bool Changed)> MapAsync(
        ReadingGoal goal,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var currentValue = await repository.GetCurrentValueAsync(goal, cancellationToken);
        var changed = false;
        if (currentValue >= goal.TargetValue && goal.MarkCompleted(now))
        {
            repository.AddNotification(new Notification(
                goal.UserId,
                NotificationType.SYSTEM,
                "Hoàn thành mục tiêu đọc",
                CompletionMessage(goal),
                "/goals"));
            changed = true;
        }

        var progressPercent = Math.Clamp(
            (int)Math.Round(currentValue * 100d / goal.TargetValue),
            0,
            100);
        return (
            new ReadingGoalDto(
                goal.Id,
                goal.Metric,
                goal.Period,
                goal.TargetValue,
                currentValue,
                progressPercent,
                goal.StartDate,
                goal.EndDate,
                goal.StatusAt(now),
                goal.CompletedAt,
                goal.CreatedAt,
                goal.UpdatedAt),
            changed);
    }

    private static string CompletionMessage(ReadingGoal goal)
    {
        var unit = goal.Metric switch
        {
            ReadingGoalMetric.BOOKS => "cuốn sách",
            ReadingGoalMetric.PAGES => "trang",
            ReadingGoalMetric.MINUTES => "phút đọc",
            _ => "đơn vị"
        };
        return $"Bạn đã hoàn thành mục tiêu {goal.TargetValue:N0} {unit}.";
    }

    private static void ValidateWritableDate(DateTimeOffset endDate)
    {
        if (endDate <= DateTimeOffset.UtcNow)
        {
            throw ServiceErrors.BadRequest(
                "INVALID_READING_GOAL_DATE",
                "Ngày kết thúc mục tiêu phải nằm trong tương lai.");
        }
    }

    private static void ValidateGoalDefinition(
        ReadingGoalMetric metric,
        ReadingGoalPeriod period)
    {
        if (!Enum.IsDefined(metric))
        {
            throw ServiceErrors.BadRequest(
                "INVALID_READING_GOAL_METRIC",
                "Loại chỉ số mục tiêu đọc không hợp lệ.");
        }

        if (!Enum.IsDefined(period))
        {
            throw ServiceErrors.BadRequest(
                "INVALID_READING_GOAL_PERIOD",
                "Chu kỳ mục tiêu đọc không hợp lệ.");
        }
    }
}
