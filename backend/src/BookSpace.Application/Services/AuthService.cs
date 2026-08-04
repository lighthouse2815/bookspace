using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;

namespace BookSpace.Application.Services;

public sealed class AuthService(
    IBookSpaceDbContext db,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer,
    IPasswordResetTokenIssuer passwordResetTokenIssuer,
    IPasswordResetEmailSender passwordResetEmailSender,
    IAuthMutationBoundary mutationBoundary,
    TimeProvider timeProvider) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        ValidatePassword(request.Password);
        if (db.Users.Any(x => x.Email == email))
        {
            throw ServiceErrors.Conflict("EMAIL_ALREADY_EXISTS", "Email đã được sử dụng.");
        }

        var user = new User(email, passwordHasher.Hash(request.Password), request.DisplayName);
        db.Add(user);
        var issued = tokenIssuer.Issue(user);
        db.Add(new RefreshToken(user.Id, issued.RefreshTokenHash, issued.RefreshExpiresAt));
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(user, issued);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = db.Users.FirstOrDefault(x => x.Email == email);
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw ServiceErrors.Unauthorized("INVALID_CREDENTIALS", "Email hoặc mật khẩu không chính xác.");
        }

        user.EnsureCanLogin();
        var issued = tokenIssuer.Issue(user);
        db.Add(new RefreshToken(user.Id, issued.RefreshTokenHash, issued.RefreshExpiresAt));
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(user, issued);
    }

    public async Task RequestPasswordResetAsync(
        RequestPasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var pending = await mutationBoundary.ExecuteAsync(
            async innerCancellationToken =>
            {
                var user = db.Users.FirstOrDefault(x => x.Email == normalizedEmail);
                if (user is null || user.IsLocked)
                {
                    return null;
                }

                var now = timeProvider.GetUtcNow();
                var latest = db.PasswordResetTokens
                    .Where(x => x.UserId == user.Id)
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefault();
                if (latest is not null &&
                    latest.CreatedAt > now.Subtract(passwordResetTokenIssuer.RequestCooldown))
                {
                    return null;
                }

                foreach (var activeToken in db.PasswordResetTokens
                             .Where(x => x.UserId == user.Id)
                             .ToList()
                             .Where(x => x.IsActiveAt(now)))
                {
                    activeToken.Invalidate(now);
                }

                var issued = passwordResetTokenIssuer.Issue();
                var resetToken = new PasswordResetToken(
                    user.Id,
                    issued.TokenHash,
                    issued.ExpiresAt);
                db.Add(resetToken);
                await db.SaveChangesAsync(innerCancellationToken);
                return new PendingPasswordResetEmail(
                    resetToken.Id,
                    user.Email,
                    user.DisplayName,
                    issued.Token,
                    issued.ExpiresAt);
            },
            cancellationToken);

        if (pending is null)
        {
            return;
        }

        var delivered = false;
        try
        {
            delivered = await passwordResetEmailSender.SendAsync(
                new PasswordResetEmail(
                    pending.RecipientEmail,
                    pending.RecipientDisplayName,
                    pending.Token,
                    pending.ExpiresAt),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // The public response remains enumeration-safe. The unusable token is
            // invalidated below and the delivery implementation owns diagnostics.
        }

        if (!delivered)
        {
            await mutationBoundary.ExecuteAsync(
                async innerCancellationToken =>
                {
                    var token = db.PasswordResetTokens.FirstOrDefault(x => x.Id == pending.TokenId);
                    token?.Invalidate(timeProvider.GetUtcNow());
                    await db.SaveChangesAsync(innerCancellationToken);
                    return true;
                },
                cancellationToken);
        }
    }

    public Task ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        ValidatePassword(request.Password);
        var tokenHash = passwordResetTokenIssuer.Hash(request.Token.Trim());
        return mutationBoundary.ExecuteAsync(
            async innerCancellationToken =>
            {
                var now = timeProvider.GetUtcNow();
                var resetToken = db.PasswordResetTokens.FirstOrDefault(x => x.TokenHash == tokenHash);
                if (resetToken is null || !resetToken.IsActiveAt(now))
                {
                    throw InvalidPasswordResetToken();
                }

                var user = db.Users.FirstOrDefault(x => x.Id == resetToken.UserId);
                if (user is null || user.IsLocked)
                {
                    throw InvalidPasswordResetToken();
                }

                resetToken.Use(now);
                foreach (var siblingToken in db.PasswordResetTokens
                             .Where(x => x.UserId == user.Id && x.Id != resetToken.Id)
                             .ToList()
                             .Where(x => x.IsActiveAt(now)))
                {
                    siblingToken.Invalidate(now);
                }

                user.ChangePasswordHash(passwordHasher.Hash(request.Password));
                foreach (var refreshToken in db.RefreshTokens
                             .Where(x => x.UserId == user.Id)
                             .ToList()
                             .Where(x => x.IsActive))
                {
                    refreshToken.Revoke();
                }

                await db.SaveChangesAsync(innerCancellationToken);
                return true;
            },
            cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = tokenIssuer.HashRefreshToken(request.RefreshToken);
        var current = db.RefreshTokens.FirstOrDefault(x => x.TokenHash == tokenHash);
        if (current is null || !current.IsActive)
        {
            throw ServiceErrors.Unauthorized("INVALID_REFRESH_TOKEN", "Refresh token không hợp lệ hoặc đã hết hạn.");
        }

        var user = db.Users.FirstOrDefault(x => x.Id == current.UserId)
                   ?? throw ServiceErrors.Unauthorized("ACCOUNT_UNAVAILABLE", "Tài khoản không còn tồn tại.");
        user.EnsureCanLogin();
        var issued = tokenIssuer.Issue(user);
        var replacement = new RefreshToken(user.Id, issued.RefreshTokenHash, issued.RefreshExpiresAt);
        db.Add(replacement);
        current.Revoke(replacement.Id);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(user, issued);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var tokenHash = tokenIssuer.HashRefreshToken(request.RefreshToken);
        var current = db.RefreshTokens.FirstOrDefault(x => x.TokenHash == tokenHash);
        if (current is not null)
        {
            current.Revoke();
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public UserSummary GetMe(Guid userId)
    {
        var user = db.Users.FirstOrDefault(x => x.Id == userId)
                   ?? throw ServiceErrors.NotFound("USER_NOT_FOUND", "Không tìm thấy người dùng.");
        return new UserSummary(user.Id, user.Email, user.DisplayName, user.AvatarUrl, user.Role);
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length < 8 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            password.All(char.IsLetterOrDigit))
        {
            throw new UseCaseException(
                "WEAK_PASSWORD",
                "Mật khẩu phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.");
        }
    }

    private static UseCaseException InvalidPasswordResetToken() =>
        new(
            "PASSWORD_RESET_TOKEN_INVALID",
            "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.",
            400);

    private sealed record PendingPasswordResetEmail(
        Guid TokenId,
        string RecipientEmail,
        string RecipientDisplayName,
        string Token,
        DateTimeOffset ExpiresAt);

    private static AuthResponse ToResponse(User user, IssuedTokens issued) =>
        new(
            issued.AccessToken,
            issued.RefreshToken,
            issued.ExpiresAt,
            new UserSummary(user.Id, user.Email, user.DisplayName, user.AvatarUrl, user.Role));
}
