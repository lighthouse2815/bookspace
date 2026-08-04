using BookSpace.Domain.Entities;
using BookSpace.Domain.Exceptions;

namespace BookSpace.UnitTests;

public sealed class PasswordRecoveryDomainTests
{
    [Fact]
    public void Password_reset_token_is_one_time_and_can_be_invalidated()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new PasswordResetToken(
            Guid.NewGuid(),
            new string('A', 64),
            now.AddMinutes(15));

        Assert.True(token.IsActiveAt(now));
        token.Use(now.AddMinutes(1));
        Assert.False(token.IsActiveAt(now.AddMinutes(1)));
        Assert.Throws<DomainException>(() => token.Use(now.AddMinutes(2)));

        var invalidated = new PasswordResetToken(
            Guid.NewGuid(),
            new string('B', 64),
            now.AddMinutes(15));
        invalidated.Invalidate(now.AddMinutes(1));
        Assert.False(invalidated.IsActiveAt(now.AddMinutes(1)));
        Assert.Throws<DomainException>(() => invalidated.Use(now.AddMinutes(2)));
    }

    [Fact]
    public void Changing_password_advances_auth_version()
    {
        var user = new User("reader@example.com", "old-hash", "Độc giả");

        user.ChangePasswordHash("new-hash");

        Assert.Equal("new-hash", user.PasswordHash);
        Assert.Equal(1, user.AuthVersion);
    }
}
