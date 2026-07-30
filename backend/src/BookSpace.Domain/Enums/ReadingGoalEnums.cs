namespace BookSpace.Domain.Enums;

public enum ReadingGoalMetric
{
    BOOKS,
    PAGES,
    MINUTES
}

public enum ReadingGoalPeriod
{
    WEEK,
    MONTH,
    YEAR,
    CUSTOM
}

public enum ReadingGoalStatus
{
    ACTIVE,
    COMPLETED,
    EXPIRED
}
