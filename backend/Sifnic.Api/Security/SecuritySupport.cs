using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Sifnic.Api.Security;

public static class SecuritySupport
{
    public const string DefaultAlgorithm = "PBKDF2SHA1";
    public const int DefaultIterations = 100000;

    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string HashPassword(string password)
    {
        var safePassword = password ?? string.Empty;
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            safePassword,
            salt,
            DefaultIterations,
            HashAlgorithmName.SHA1,
            HashSize);

        return string.Join(
            "|",
            DefaultAlgorithm,
            DefaultIterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var parts = storedHash.Split('|', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        var algorithm = parts[0].ToUpperInvariant();
        var salt = Convert.FromBase64String(parts[2]);
        var expectedHash = Convert.FromBase64String(parts[3]);

        var actualHash = algorithm switch
        {
            "PBKDF2SHA1" => Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA1, expectedHash.Length),
            "PBKDF2SHA256" => Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length),
            "PBKDF2SHA512" => Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA512, expectedHash.Length),
            _ => Array.Empty<byte>(),
        };

        return actualHash.Length == expectedHash.Length &&
               CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    public static string GenerateUniqueUsername(
        string? nombres,
        string? apellidos,
        Func<string, bool> usernameExists)
    {
        var nameTokens = SplitWords(nombres);
        var surnameTokens = SplitWords(apellidos);

        var firstInitial = nameTokens.Count > 0 ? nameTokens[0][0].ToString() : "u";
        var primarySurname = surnameTokens.Count > 0
            ? surnameTokens[0]
            : nameTokens.Count > 1
                ? nameTokens[1]
                : nameTokens.Count > 0
                    ? nameTokens[0]
                    : "usuario";

        var baseUsername = NormalizeUsername($"{firstInitial}{primarySurname}");
        if (string.IsNullOrWhiteSpace(baseUsername))
        {
            baseUsername = "usuario";
        }

        if (!usernameExists(baseUsername))
        {
            return baseUsername;
        }

        if (surnameTokens.Count > 1)
        {
            var extraInitials = new StringBuilder();

            for (var index = 1; index < surnameTokens.Count; index += 1)
            {
                extraInitials.Append(surnameTokens[index][0]);
                var candidate = NormalizeUsername($"{baseUsername}{extraInitials}");

                if (!usernameExists(candidate))
                {
                    return candidate;
                }
            }
        }

        for (var suffix = 2; suffix < 1000; suffix += 1)
        {
            var candidate = NormalizeUsername($"{baseUsername}{suffix}");
            if (!usernameExists(candidate))
            {
                return candidate;
            }
        }

        return $"{baseUsername}{Guid.NewGuid():N}"[..Math.Min(30, baseUsername.Length + 6)];
    }

    public static string BuildDisplayName(string? nombres, string? apellidos)
    {
        return string.Join(
            " ",
            new[] { nombres?.Trim(), apellidos?.Trim() }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    public static string NormalizeUsername(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    public static IReadOnlyList<string> SplitWords(string? value)
    {
        return NormalizeWordSequence(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }

    private static string NormalizeWordSequence(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                builder.Append(' ');
            }
        }

        return string.Join(
            ' ',
            builder.ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
