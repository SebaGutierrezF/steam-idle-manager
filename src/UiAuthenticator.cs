using SteamKit2.Authentication;

namespace SteamIdleManager;

public sealed class UiAuthenticator : IAuthenticator
{
    private readonly bool _preferGuardCode;
    private readonly Func<GuardCodePrompt, Task<string>> _guardCodeProvider;
    private readonly Action<string>? _status;

    public UiAuthenticator(
        bool preferGuardCode,
        Func<GuardCodePrompt, Task<string>> guardCodeProvider,
        Action<string>? status = null
    )
    {
        _preferGuardCode = preferGuardCode;
        _guardCodeProvider = guardCodeProvider;
        _status = status;
    }

    public Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
    {
        return _guardCodeProvider(
            new GuardCodePrompt
            {
                IsEmailCode = false,
                PreviousCodeWasIncorrect = previousCodeWasIncorrect,
            }
        );
    }

    public Task<string> GetEmailCodeAsync(
        string email,
        bool previousCodeWasIncorrect
    )
    {
        return _guardCodeProvider(
            new GuardCodePrompt
            {
                IsEmailCode = true,
                Email = email,
                PreviousCodeWasIncorrect = previousCodeWasIncorrect,
            }
        );
    }

    public Task<bool> AcceptDeviceConfirmationAsync()
    {
        if (_preferGuardCode)
        {
            _status?.Invoke(
                "Using Steam Guard code entry instead of mobile approval."
            );
            return Task.FromResult(false);
        }

        _status?.Invoke(
            "Approve the sign-in in the Steam mobile app."
        );

        return Task.FromResult(true);
    }
}
