using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SteamIdleManager;

public static class SecureSessionStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private static string BaseDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SteamIdleManager"
        );

    private static string AccountsPath =>
        Path.Combine(BaseDirectory, "accounts.json");

    private static readonly byte[] OptionalEntropy =
        Encoding.UTF8.GetBytes("SteamIdleManager.SessionStore.v1");

    public static List<SavedSteamAccount> ListAccounts()
    {
        try
        {
            if (!File.Exists(AccountsPath))
            {
                return new List<SavedSteamAccount>();
            }

            var json = File.ReadAllText(AccountsPath);
            return JsonSerializer.Deserialize<List<SavedSteamAccount>>(json, Options)
                ?.Where(a => !string.IsNullOrWhiteSpace(a.AccountName))
                .OrderBy(a => a.AccountName, StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? new List<SavedSteamAccount>();
        }
        catch
        {
            return new List<SavedSteamAccount>();
        }
    }

    public static bool TryGetSession(
        string accountName,
        out string refreshToken,
        out string? guardData
    )
    {
        refreshToken = string.Empty;
        guardData = null;

        try
        {
            var account = ListAccounts().FirstOrDefault(
                a => string.Equals(
                    a.AccountName,
                    accountName,
                    StringComparison.OrdinalIgnoreCase
                )
            );

            if (account is null)
            {
                return false;
            }

            refreshToken = Unprotect(account.ProtectedRefreshToken);

            if (!string.IsNullOrWhiteSpace(account.ProtectedGuardData))
            {
                guardData = Unprotect(account.ProtectedGuardData!);
            }

            return !string.IsNullOrWhiteSpace(refreshToken);
        }
        catch
        {
            refreshToken = string.Empty;
            guardData = null;
            return false;
        }
    }

    public static string? GetGuardData(string accountName)
    {
        return TryGetSession(accountName, out _, out var guardData)
            ? guardData
            : null;
    }

    public static void SaveSession(
        string accountName,
        string refreshToken,
        string? guardData
    )
    {
        if (
            string.IsNullOrWhiteSpace(accountName)
            || string.IsNullOrWhiteSpace(refreshToken)
        )
        {
            return;
        }

        Directory.CreateDirectory(BaseDirectory);

        var accounts = ListAccounts();
        var existing = accounts.FirstOrDefault(
            a => string.Equals(
                a.AccountName,
                accountName,
                StringComparison.OrdinalIgnoreCase
            )
        );

        if (existing is null)
        {
            existing = new SavedSteamAccount
            {
                AccountName = accountName,
            };
            accounts.Add(existing);
        }

        existing.ProtectedRefreshToken = Protect(refreshToken);
        existing.ProtectedGuardData = string.IsNullOrWhiteSpace(guardData)
            ? existing.ProtectedGuardData
            : Protect(guardData!);
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        File.WriteAllText(
            AccountsPath,
            JsonSerializer.Serialize(
                accounts
                    .OrderBy(a => a.AccountName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Options
            )
        );
    }

    public static void DeleteSession(string accountName)
    {
        try
        {
            var accounts = ListAccounts();
            accounts.RemoveAll(
                a => string.Equals(
                    a.AccountName,
                    accountName,
                    StringComparison.OrdinalIgnoreCase
                )
            );

            Directory.CreateDirectory(BaseDirectory);
            File.WriteAllText(
                AccountsPath,
                JsonSerializer.Serialize(accounts, Options)
            );
        }
        catch
        {
            // Deleting a saved convenience session must not crash the app.
        }
    }

    private static string Protect(string value)
    {
        var plain = Encoding.UTF8.GetBytes(value);

        var encrypted = ProtectedData.Protect(
            plain,
            OptionalEntropy,
            DataProtectionScope.CurrentUser
        );

        CryptographicOperations.ZeroMemory(plain);

        return Convert.ToBase64String(encrypted);
    }

    private static string Unprotect(string value)
    {
        var encrypted = Convert.FromBase64String(value);

        var plain = ProtectedData.Unprotect(
            encrypted,
            OptionalEntropy,
            DataProtectionScope.CurrentUser
        );

        try
        {
            return Encoding.UTF8.GetString(plain);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }
}
