using BookSpace.Application.Abstractions;
using BookSpace.Application.Common;
using BookSpace.Application.Contracts;
using BookSpace.Domain.Entities;

namespace BookSpace.Application.Services;

public sealed class AuthService(
    IBookSpaceDbContext db,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer) : IAuthService
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

    private static AuthResponse ToResponse(User user, IssuedTokens issued) =>
        new(
            issued.AccessToken,
            issued.RefreshToken,
            issued.ExpiresAt,
            new UserSummary(user.Id, user.Email, user.DisplayName, user.AvatarUrl, user.Role));
}
