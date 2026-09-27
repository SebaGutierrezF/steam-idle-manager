namespace SteamIdleManager;

public sealed class GameEntry
{
    public uint AppId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "game";
    public string Source { get; set; } = "Steam account";

    public override string ToString() => $"{Name} ({AppId})";
}
