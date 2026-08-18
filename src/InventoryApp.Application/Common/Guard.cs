namespace InventoryApp.Application.Common;

/// <summary>Small validation helpers so services fail fast with a clean message.</summary>
public static class Guard
{
    public static string Required(string? value, string field, int maxLength = 200)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new ValidationException($"{field} is required.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new ValidationException($"{field} cannot be longer than {maxLength} characters.");
        }

        return trimmed;
    }

    public static string Optional(string? value, string field, int maxLength = 1000)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ValidationException($"{field} cannot be longer than {maxLength} characters.");
        }

        return trimmed;
    }

    public static void Positive(int value, string field)
    {
        if (value <= 0)
        {
            throw new ValidationException($"{field} must be greater than zero.");
        }
    }

    public static void NotNegative(decimal value, string field)
    {
        if (value < 0)
        {
            throw new ValidationException($"{field} cannot be negative.");
        }
    }

    public static void NotNegative(int value, string field)
    {
        if (value < 0)
        {
            throw new ValidationException($"{field} cannot be negative.");
        }
    }

    public static void Email(string? value, string field)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        var at = trimmed.IndexOf('@');
        if (at <= 0 || at == trimmed.Length - 1 || trimmed.IndexOf('.', at) < 0 || trimmed.Contains(' '))
        {
            throw new ValidationException($"{field} is not a valid email address.");
        }
    }
}
