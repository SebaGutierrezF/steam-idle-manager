using System.Text.Json;

namespace SteamIdleManager;

public sealed class IdleProfile
{
    public string Name { get; set; } = string.Empty;
    public List<uint> AppIds { get; set; } = new();
    public bool Indefinite { get; set; } = true;
    public int DurationMinutes { get; set; } = 60;
    public bool PublicPresence { get; set; }
    public uint? PreferredPublicAppId { get; set; }
}

public sealed class AccountPreferences
{
    public List<IdleProfile> Profiles { get; set; } = new();
    public List<uint> FavoriteAppIds { get; set; } = new();
    public List<uint> RecentAppIds { get; set; } = new();
    public bool MinimizeToTray { get; set; } = true;
}

public static class PreferencesStore
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

    private static string PathFor(string accountName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(
            accountName
                .Select(ch => invalid.Contains(ch) ? '_' : ch)
                .ToArray()
        );

        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "unknown";
        }

        return Path.Combine(BaseDirectory, $"preferences-{safe}.json");
    }

    public static AccountPreferences Load(string accountName)
    {
        try
        {
            var path = PathFor(accountName);

            if (!File.Exists(path))
            {
                return new AccountPreferences();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AccountPreferences>(json, Options)
                ?? new AccountPreferences();
        }
        catch
        {
            return new AccountPreferences();
        }
    }

    public static void Save(
        string accountName,
        AccountPreferences preferences
    )
    {
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(BaseDirectory);

            preferences.FavoriteAppIds = preferences.FavoriteAppIds
                .Distinct()
                .ToList();

            preferences.RecentAppIds = preferences.RecentAppIds
                .Distinct()
                .Take(30)
                .ToList();

            preferences.Profiles = preferences.Profiles
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .GroupBy(
                    p => p.Name.Trim(),
                    StringComparer.OrdinalIgnoreCase
                )
                .Select(g => g.Last())
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            File.WriteAllText(
                PathFor(accountName),
                JsonSerializer.Serialize(preferences, Options)
            );
        }
        catch
        {
            // UX preferences must never interrupt idling.
        }
    }
}
