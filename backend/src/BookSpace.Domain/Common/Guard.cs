using System.Net.Mail;
using BookSpace.Domain.Exceptions;

namespace BookSpace.Domain.Common;

internal static class Guard
{
    public static string Required(string? value, string field, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainException("VALIDATION_ERROR", $"{field} không được để trống.");
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainException("VALIDATION_ERROR", $"{field} không được vượt quá {maxLength} ký tự.");
        }

        return normalized;
    }

    public static string? Optional(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new DomainException("VALIDATION_ERROR", $"{field} không được vượt quá {maxLength} ký tự.");
        }

        return normalized;
    }

    public static string Email(string? value)
    {
        var email = Required(value, "Email", 254).ToLowerInvariant();
        try
        {
            _ = new MailAddress(email);
        }
        catch (FormatException)
        {
            throw new DomainException("INVALID_EMAIL", "Địa chỉ email không hợp lệ.");
        }

        return email;
    }

    public static int Positive(int value, string field)
    {
        if (value <= 0)
        {
            throw new DomainException("VALIDATION_ERROR", $"{field} phải lớn hơn 0.");
        }

        return value;
    }

    public static int Range(int value, int min, int max, string field)
    {
        if (value < min || value > max)
        {
            throw new DomainException("VALIDATION_ERROR", $"{field} phải nằm trong khoảng {min} đến {max}.");
        }

        return value;
    }
}
