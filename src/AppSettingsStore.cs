using System.Text.Json;
using Microsoft.Win32;

namespace SteamIdleManager;

public sealed class AppSettings
{
    public bool AutoConnectEnabled { get; set; }
    public string AutoConnectAccount { get; set; } = string.Empty;

    public bool AutoStartProfileEnabled { get; set; }
    public string AutoStartProfileName { get; set; } = string.Empty;

    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
}

public static class AppSettingsStore
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

    private static string SettingsPath =>
        Path.Combine(BaseDirectory, "app-settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsPath);

            return JsonSerializer.Deserialize<AppSettings>(json, Options)
                ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(BaseDirectory);

            File.WriteAllText(
                SettingsPath,
                JsonSerializer.Serialize(settings, Options)
            );
        }
        catch
        {
            // Settings persistence must never break the app.
        }
    }
}

public static class WindowsStartupManager
{
    private const string RunKey =
        @"Software\Microsoft\Windows\CurrentVersion\Run";

    private const string ValueName = "SteamIdleManager";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(ValueName) is string value
                && !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);

            if (enabled)
            {
                var executable = Application.ExecutablePath;

                key.SetValue(
                    ValueName,
                    $"\"{executable}\""
                );
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
