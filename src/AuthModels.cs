namespace SteamIdleManager;

public enum SteamLoginMethod
{
    Qr,
    Credentials,
    SavedSession,
}

public sealed class SteamLoginRequest
{
    public SteamLoginMethod Method { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public bool RememberSession { get; init; }
    public bool PreferGuardCode { get; init; } = true;
}

public sealed class SavedSteamAccount
{
    public string AccountName { get; set; } = string.Empty;
    public string ProtectedRefreshToken { get; set; } = string.Empty;
    public string? ProtectedGuardData { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class GuardCodePrompt
{
    public bool IsEmailCode { get; init; }
    public string Email { get; init; } = string.Empty;
    public bool PreviousCodeWasIncorrect { get; init; }
}
