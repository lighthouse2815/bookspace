using System.ComponentModel.DataAnnotations;
using BookSpace.Domain.Enums;

namespace BookSpace.Application.Contracts;

public sealed record ReadingGoalDto(
    Guid Id,
    ReadingGoalMetric Metric,
    ReadingGoalPeriod Period,
    int TargetValue,
    int CurrentValue,
    int ProgressPercent,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    ReadingGoalStatus Status,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateReadingGoalRequest(
    ReadingGoalMetric Metric,
    ReadingGoalPeriod Period,
    [Range(1, 1_000_000)] int TargetValue,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate);

public sealed record UpdateReadingGoalRequest(
    ReadingGoalMetric Metric,
    ReadingGoalPeriod Period,
    [Range(1, 1_000_000)] int TargetValue,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate);
