using System.Text.Json;

namespace SteamIdleManager;

public static class LibraryCache
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private static string DataDirectory =>
        Path.Combine(AppContext.BaseDirectory, "data");

    private static string CachePath(string accountName)
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

        return Path.Combine(DataDirectory, $"library-{safe}.json");
    }

    public static List<GameEntry> Load(string accountName)
    {
        try
        {
            var path = CachePath(accountName);

            if (!File.Exists(path))
            {
                return new List<GameEntry>();
            }

            var json = File.ReadAllText(path);

            return JsonSerializer.Deserialize<List<GameEntry>>(json, Options)
                ?? new List<GameEntry>();
        }
        catch
        {
            return new List<GameEntry>();
        }
    }

    public static void Save(
        string accountName,
        IEnumerable<GameEntry> games
    )
    {
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(DataDirectory);

            var ordered = games
                .GroupBy(g => g.AppId)
                .Select(g => g.First())
                .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            File.WriteAllText(
                CachePath(accountName),
                JsonSerializer.Serialize(ordered, Options)
            );
        }
        catch
        {
            // Cache failures should never stop idling.
        }
    }
}
