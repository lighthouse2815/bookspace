using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using BookSpace.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BookSpace.Infrastructure.Security;

public sealed class PasswordRecoveryOptions
{
    public const string SectionName = "PasswordRecovery";

    public int TokenLifetimeMinutes { get; init; } = 15;
    public int RequestCooldownSeconds { get; init; } = 60;
    public string FrontendResetUrl { get; init; } = "http://localhost:5173/reset-password";
    public string DeliveryMode { get; init; } = "Disabled";
    public SmtpPasswordRecoveryOptions Smtp { get; init; } = new();

    public bool IsValid()
    {
        if (TokenLifetimeMinutes is < 5 or > 60 ||
            RequestCooldownSeconds is < 0 or > 3600 ||
            !Uri.TryCreate(FrontendResetUrl, UriKind.Absolute, out var resetUri) ||
            resetUri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        var normalizedMode = DeliveryMode.Trim().ToUpperInvariant();
        if (normalizedMode is not ("DISABLED" or "LOG" or "SMTP"))
        {
            return false;
        }

        return normalizedMode != "SMTP" || Smtp.IsValid();
    }
}

public sealed class SmtpPasswordRecoveryOptions
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool EnableSsl { get; init; } = true;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = "BookSpace";

    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Host) ||
            Port is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(FromAddress) ||
            FromName.Length > 200 ||
            (!string.IsNullOrWhiteSpace(Username) && string.IsNullOrWhiteSpace(Password)))
        {
            return false;
        }

        try
        {
            _ = new MailAddress(FromAddress);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed class CryptographicPasswordResetTokenIssuer(
    IOptions<PasswordRecoveryOptions> options,
    TimeProvider timeProvider) : IPasswordResetTokenIssuer
{
    private readonly PasswordRecoveryOptions _options = options.Value;

    public TimeSpan RequestCooldown =>
        TimeSpan.FromSeconds(_options.RequestCooldownSeconds);

    public IssuedPasswordResetToken Issue()
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48));
        return new IssuedPasswordResetToken(
            token,
            Hash(token),
            timeProvider.GetUtcNow().AddMinutes(_options.TokenLifetimeMinutes));
    }

    public string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public sealed class ConfiguredPasswordResetEmailSender(
    IOptions<PasswordRecoveryOptions> options,
    IHostEnvironment environment,
    ILogger<ConfiguredPasswordResetEmailSender> logger) : IPasswordResetEmailSender
{
    private readonly PasswordRecoveryOptions _options = options.Value;

    public async Task<bool> SendAsync(
        PasswordResetEmail email,
        CancellationToken cancellationToken)
    {
        var resetLink = BuildResetLink(email.Token);
        var deliveryMode = _options.DeliveryMode.Trim().ToUpperInvariant();
        if (deliveryMode == "DISABLED")
        {
            logger.LogWarning(
                "Password recovery delivery is disabled; no email was sent to {Recipient}.",
                email.RecipientEmail);
            return false;
        }

        if (deliveryMode == "LOG")
        {
            if (!environment.IsDevelopment())
            {
                logger.LogError(
                    "PasswordRecovery:DeliveryMode=Log is allowed only in Development; no email was sent.");
                return false;
            }

            logger.LogWarning(
                "Development password reset for {Recipient}. Link valid until {ExpiresAt}: {ResetLink}",
                email.RecipientEmail,
                email.ExpiresAt,
                resetLink);
            return true;
        }

        return await SendSmtpAsync(email, resetLink, cancellationToken);
    }

    private async Task<bool> SendSmtpAsync(
        PasswordResetEmail email,
        string resetLink,
        CancellationToken cancellationToken)
    {
        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(
                    _options.Smtp.FromAddress,
                    _options.Smtp.FromName,
                    Encoding.UTF8),
                Subject = "Đặt lại mật khẩu BookSpace",
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = false,
                Body = $"Xin chào {email.RecipientDisplayName},\n\n" +
                       "Dùng liên kết sau để đặt lại mật khẩu BookSpace:\n" +
                       $"{resetLink}\n\n" +
                       $"Liên kết hết hạn lúc {email.ExpiresAt:O} và chỉ sử dụng được một lần.\n" +
                       "Nếu bạn không yêu cầu thao tác này, hãy bỏ qua email."
            };
            message.To.Add(email.RecipientEmail);

            using var client = new SmtpClient(_options.Smtp.Host, _options.Smtp.Port)
            {
                EnableSsl = _options.Smtp.EnableSsl
            };
            if (!string.IsNullOrWhiteSpace(_options.Smtp.Username))
            {
                client.Credentials = new NetworkCredential(
                    _options.Smtp.Username,
                    _options.Smtp.Password);
            }

            await client.SendMailAsync(message, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Could not deliver password reset email to {Recipient}.",
                email.RecipientEmail);
            return false;
        }
    }

    private string BuildResetLink(string token)
    {
        var separator = _options.FrontendResetUrl.Contains('?')
            ? '&'
            : '?';
        return $"{_options.FrontendResetUrl}{separator}token={Uri.EscapeDataString(token)}";
    }
}
