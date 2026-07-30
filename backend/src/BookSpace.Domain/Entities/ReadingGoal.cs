using BookSpace.Domain.Common;
using BookSpace.Domain.Enums;
using BookSpace.Domain.Exceptions;

namespace BookSpace.Domain.Entities;

public sealed class ReadingGoal : Entity
{
    private const int MaximumTargetValue = 1_000_000;
    private static readonly TimeSpan MaximumDuration = TimeSpan.FromDays(366);

    private ReadingGoal() { }

    public ReadingGoal(
        Guid userId,
        ReadingGoalMetric metric,
        ReadingGoalPeriod period,
        int targetValue,
        DateTimeOffset startDate,
        DateTimeOffset endDate)
    {
        UserId = userId;
        Apply(metric, period, targetValue, startDate, endDate, touch: false);
    }

    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public ReadingGoalMetric Metric { get; private set; }
    public ReadingGoalPeriod Period { get; private set; }
    public int TargetValue { get; private set; }
    public DateTimeOffset StartDate { get; private set; }
    public DateTimeOffset EndDate { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public void Update(
        ReadingGoalMetric metric,
        ReadingGoalPeriod period,
        int targetValue,
        DateTimeOffset startDate,
        DateTimeOffset endDate)
    {
        if (CompletedAt.HasValue)
        {
            throw new DomainException(
                "READING_GOAL_ALREADY_COMPLETED",
                "Không thể thay đổi mục tiêu đã hoàn thành.");
        }

        if (EndDate < DateTimeOffset.UtcNow)
        {
            throw new DomainException(
                "READING_GOAL_ALREADY_EXPIRED",
                "Không thể thay đổi mục tiêu đã hết hạn.");
        }

        Apply(metric, period, targetValue, startDate, endDate, touch: true);
    }

    public bool MarkCompleted(DateTimeOffset completedAt)
    {
        if (CompletedAt.HasValue)
        {
            return false;
        }

        CompletedAt = completedAt;
        Touch();
        return true;
    }

    public ReadingGoalStatus StatusAt(DateTimeOffset now) =>
        CompletedAt.HasValue
            ? ReadingGoalStatus.COMPLETED
            : now > EndDate
                ? ReadingGoalStatus.EXPIRED
                : ReadingGoalStatus.ACTIVE;

    private void Apply(
        ReadingGoalMetric metric,
        ReadingGoalPeriod period,
        int targetValue,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        bool touch)
    {
        if (!Enum.IsDefined(metric))
        {
            throw new DomainException(
                "INVALID_READING_GOAL_METRIC",
                "Loại chỉ số mục tiêu đọc không hợp lệ.");
        }

        if (!Enum.IsDefined(period))
        {
            throw new DomainException(
                "INVALID_READING_GOAL_PERIOD",
                "Chu kỳ mục tiêu đọc không hợp lệ.");
        }

        if (targetValue < 1 || targetValue > MaximumTargetValue)
        {
            throw new DomainException(
                "INVALID_READING_GOAL_TARGET",
                $"Giá trị mục tiêu phải từ 1 đến {MaximumTargetValue:N0}.");
        }

        if (endDate <= startDate)
        {
            throw new DomainException(
                "INVALID_READING_GOAL_DATE",
                "Ngày kết thúc mục tiêu phải sau ngày bắt đầu.");
        }

        if (endDate - startDate > MaximumDuration)
        {
            throw new DomainException(
                "INVALID_READING_GOAL_DATE",
                "Thời gian của mục tiêu không được vượt quá 366 ngày.");
        }

        Metric = metric;
        Period = period;
        TargetValue = targetValue;
        StartDate = startDate;
        EndDate = endDate;

        if (touch)
        {
            Touch();
        }
    }
}
