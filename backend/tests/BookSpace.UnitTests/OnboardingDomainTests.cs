using BookSpace.Domain.Entities;
using BookSpace.Domain.Enums;

namespace BookSpace.UnitTests;

public sealed class OnboardingDomainTests
{
    [Fact]
    public void New_user_starts_with_pending_onboarding()
    {
        var user = new User("new-reader@bookspace.local", "hash", "Độc giả mới");

        Assert.Equal(OnboardingStatus.PENDING, user.OnboardingStatus);
        Assert.Null(user.OnboardingFinishedAt);
    }

    [Fact]
    public void Skip_is_idempotent_and_completion_can_replace_skipped_state()
    {
        var user = new User("skip-reader@bookspace.local", "hash", "Độc giả bỏ qua");

        user.SkipOnboarding();
        var skippedAt = Assert.IsType<DateTimeOffset>(user.OnboardingFinishedAt);
        user.SkipOnboarding();

        Assert.Equal(OnboardingStatus.SKIPPED, user.OnboardingStatus);
        Assert.Equal(skippedAt, user.OnboardingFinishedAt);

        user.CompleteOnboarding();

        Assert.Equal(OnboardingStatus.COMPLETED, user.OnboardingStatus);
        Assert.True(user.OnboardingFinishedAt >= skippedAt);
    }

    [Fact]
    public void Completed_onboarding_is_not_downgraded_by_skip_and_keeps_its_timestamp()
    {
        var user = new User("complete-reader@bookspace.local", "hash", "Độc giả hoàn tất");
        user.CompleteOnboarding();
        var completedAt = Assert.IsType<DateTimeOffset>(user.OnboardingFinishedAt);

        user.CompleteOnboarding();
        user.SkipOnboarding();

        Assert.Equal(OnboardingStatus.COMPLETED, user.OnboardingStatus);
        Assert.Equal(completedAt, user.OnboardingFinishedAt);
    }
}
