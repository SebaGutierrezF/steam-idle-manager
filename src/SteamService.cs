using QRCoder;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Internal;

namespace SteamIdleManager;

public sealed class SteamService : IDisposable
{
    private const string AppName = "Steam Idle Manager";
    private const string Version = "1.0.2";
    public const int MaxIdleApps = 32;

    private readonly SteamClient _steamClient;
    private readonly CallbackManager _callbacks;
    private readonly SteamUser _steamUser;
    private readonly SteamFriends _steamFriends;
    private readonly SteamApps _steamApps;

    private readonly CancellationTokenSource _callbackCts = new();
    private readonly SemaphoreSlim _librarySyncLock = new(1, 1);

    private Task? _callbackTask;
    private uint _loginId;

    private const int MaxCmRetries = 5;

    private bool _disposed;
    private bool _accountInfoReceived;
    private bool _idling;
    private bool _cmRetryInProgress;
    private int _cmRetryCount;

    // Kept only in process memory so TryAnotherCM can retry without another QR scan.
    // These values are never written to disk.
    private string? _sessionAccountName;
    private string? _sessionAccessToken;
    private bool _rememberSession;
    private bool _usingSavedSession;
    private SteamLoginRequest? _pendingLogin;
    private bool _accountChangedSent;

    private const int MaxAutoReconnectAttempts = 8;
    private static readonly int[] AutoReconnectDelaysSeconds =
        { 2, 3, 5, 8, 13, 20, 30, 30 };

    private CancellationTokenSource? _autoReconnectCts;
    private bool _autoReconnectInProgress;
    private int _autoReconnectAttempt;

    private bool _idlePublicPresence;
    private uint? _idlePreferredPublicAppId;

    private List<uint> _activeAppIds = new();

    public bool IsConnected { get; private set; }
    public bool IsLoggedOn { get; private set; }
    public bool IsIdling => _idling;
    public bool IsReconnecting => _autoReconnectInProgress;
    public string? CurrentAccountName => _sessionAccountName;
    public IReadOnlyList<uint> ActiveAppIds => _activeAppIds;

    public Func<GuardCodePrompt, Task<string>>? GuardCodeProvider { get; set; }

    public event Action<string>? StatusChanged;
    public event Action<byte[]>? QrReady;
    public event Action? QrApproved;
    public event Action<List<GameEntry>>? LibraryUpdated;
    public event Action<string>? ErrorOccurred;
    public event Action<bool>? LoginStateChanged;
    public event Action<bool>? IdleStateChanged;
    public event Action<string>? AccountChanged;
    public event Action<bool, int>? ReconnectStateChanged;

    public SteamService()
    {
        _steamClient = new SteamClient("SteamIdleManager-v1.0.2");
        _callbacks = new CallbackManager(_steamClient);

        _steamUser = _steamClient.GetHandler<SteamUser>()
            ?? throw new InvalidOperationException("SteamUser handler unavailable.");

        _steamFriends = _steamClient.GetHandler<SteamFriends>()
            ?? throw new InvalidOperationException("SteamFriends handler unavailable.");

        _steamApps = _steamClient.GetHandler<SteamApps>()
            ?? throw new InvalidOperationException("SteamApps handler unavailable.");

        _callbacks.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        _callbacks.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        _callbacks.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        _callbacks.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
        _callbacks.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);
        _callbacks.Subscribe<SteamUser.PlayingSessionStateCallback>(
            OnPlayingSessionState
        );
        _callbacks.Subscribe<SteamApps.LicenseListCallback>(OnLicenseList);
    }

    public void Connect(SteamLoginRequest request)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SteamService));
        }

        if (IsConnected || IsLoggedOn)
        {
            StatusChanged?.Invoke("Steam is already connected.");
            return;
        }

        _pendingLogin = request
            ?? throw new ArgumentNullException(nameof(request));

        CancelAutoReconnect();

        _rememberSession = request.RememberSession;
        _usingSavedSession = request.Method == SteamLoginMethod.SavedSession;
        _sessionAccountName = null;
        _sessionAccessToken = null;
        _accountChangedSent = false;

        if (_callbackTask is null)
        {
            _callbackTask = Task.Run(CallbackLoopAsync);
        }

        _loginId = unchecked(
            (uint)Random.Shared.NextInt64(1, (long)uint.MaxValue)
        );

        AppLogger.Info("Connecting to Steam.");
        StatusChanged?.Invoke("Connecting to Steam...");
        _steamClient.Connect();
    }

    private async Task CallbackLoopAsync()
    {
        try
        {
            while (!_callbackCts.Token.IsCancellationRequested)
            {
                await _callbacks.RunWaitCallbackAsync(_callbackCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(
                $"Steam callback loop failed: {ex.GetType().Name}: {ex.Message}"
            );
        }
    }

    private async void OnConnected(SteamClient.ConnectedCallback callback)
    {
        IsConnected = true;

        if (
            _autoReconnectInProgress
            && !_cmRetryInProgress
            && !string.IsNullOrWhiteSpace(_sessionAccountName)
            && !string.IsNullOrWhiteSpace(_sessionAccessToken)
        )
        {
            StatusChanged?.Invoke(
                $"Steam transport restored. Logging back in "
                + $"({_autoReconnectAttempt}/{MaxAutoReconnectAttempts})..."
            );

            LogOnWithInMemorySession();
            return;
        }

        if (
            _cmRetryInProgress
            && !string.IsNullOrWhiteSpace(_sessionAccountName)
            && !string.IsNullOrWhiteSpace(_sessionAccessToken)
        )
        {
            StatusChanged?.Invoke(
                $"Connected to alternate Steam CM. Retrying Steam3 login "
                + $"({_cmRetryCount}/{MaxCmRetries})..."
            );

            LogOnWithInMemorySession();
            return;
        }

        if (_pendingLogin is null)
        {
            ErrorOccurred?.Invoke("No Steam login method was selected.");
            Disconnect();
            return;
        }

        try
        {
            switch (_pendingLogin.Method)
            {
                case SteamLoginMethod.Qr:
                    await AuthenticateWithQrAsync(_pendingLogin);
                    break;

                case SteamLoginMethod.Credentials:
                    await AuthenticateWithCredentialsAsync(_pendingLogin);
                    break;

                case SteamLoginMethod.SavedSession:
                    AuthenticateWithSavedSession(_pendingLogin.Username);
                    break;

                default:
                    throw new InvalidOperationException("Unknown Steam login method.");
            }
        }
        catch (TaskCanceledException)
        {
            ErrorOccurred?.Invoke("Steam authentication was cancelled or expired.");
            Disconnect();
        }
        catch (AuthenticationException ex)
        {
            ErrorOccurred?.Invoke(
                $"Steam authentication failed: {ex.Message} ({ex.Result})"
            );
            Disconnect();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(
                $"Steam authentication failed: {ex.GetType().Name}: {ex.Message}"
            );
            Disconnect();
        }
    }

    private async Task AuthenticateWithQrAsync(SteamLoginRequest request)
    {
        StatusChanged?.Invoke("Connected to Steam. Starting QR authentication...");

        var authSession =
            await _steamClient.Authentication.BeginAuthSessionViaQRAsync(
                new AuthSessionDetails
                {
                    DeviceFriendlyName = $"{AppName} v{Version}",
                    IsPersistentSession = request.RememberSession,
                }
            );

        authSession.ChallengeURLChanged = () =>
        {
            QrReady?.Invoke(BuildQrPng(authSession.ChallengeURL));
        };

        QrReady?.Invoke(BuildQrPng(authSession.ChallengeURL));

        var result = await authSession.PollingWaitForResultAsync();

        QrApproved?.Invoke();

        CompleteAuthenticationResult(
            result.AccountName,
            result.RefreshToken,
            result.NewGuardData,
            request.RememberSession
        );

        StatusChanged?.Invoke(
            $"QR approved for {result.AccountName}. Logging into Steam3..."
        );

        LogOnWithInMemorySession();
    }

    private async Task AuthenticateWithCredentialsAsync(
        SteamLoginRequest request
    )
    {
        if (
            string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Password)
        )
        {
            throw new InvalidOperationException(
                "Steam account name and password are required."
            );
        }

        if (GuardCodeProvider is null)
        {
            throw new InvalidOperationException(
                "Steam Guard code UI is not available."
            );
        }

        StatusChanged?.Invoke(
            $"Authenticating '{request.Username}' with Steam credentials..."
        );

        var authenticator = new UiAuthenticator(
            request.PreferGuardCode,
            GuardCodeProvider,
            message => StatusChanged?.Invoke(message)
        );

        var authSession =
            await _steamClient.Authentication.BeginAuthSessionViaCredentialsAsync(
                new AuthSessionDetails
                {
                    DeviceFriendlyName = $"{AppName} v{Version}",
                    Username = request.Username,
                    Password = request.Password,
                    IsPersistentSession = request.RememberSession,
                    GuardData = SecureSessionStore.GetGuardData(request.Username),
                    Authenticator = authenticator,
                }
            );

        var result = await authSession.PollingWaitForResultAsync();

        CompleteAuthenticationResult(
            result.AccountName,
            result.RefreshToken,
            result.NewGuardData,
            request.RememberSession
        );

        StatusChanged?.Invoke(
            $"Credentials accepted for {result.AccountName}. Logging into Steam3..."
        );

        // Do not retain the password in service state. The dialog/request object
        // may be collected after authentication completes.
        _pendingLogin = new SteamLoginRequest
        {
            Method = request.Method,
            Username = request.Username,
            RememberSession = request.RememberSession,
            PreferGuardCode = request.PreferGuardCode,
        };

        LogOnWithInMemorySession();
    }

    private void AuthenticateWithSavedSession(string accountName)
    {
        if (
            string.IsNullOrWhiteSpace(accountName)
            || !SecureSessionStore.TryGetSession(
                accountName,
                out var refreshToken,
                out _
            )
        )
        {
            throw new InvalidOperationException(
                $"No usable saved session exists for '{accountName}'."
            );
        }

        _sessionAccountName = accountName;
        _sessionAccessToken = refreshToken;
        _rememberSession = true;
        _usingSavedSession = true;

        StatusChanged?.Invoke(
            $"Using protected saved session for {accountName}..."
        );

        LogOnWithInMemorySession();
    }

    private void CompleteAuthenticationResult(
        string accountName,
        string refreshToken,
        string? guardData,
        bool rememberSession
    )
    {
        _sessionAccountName = accountName;
        _sessionAccessToken = refreshToken;
        _rememberSession = rememberSession;
        _usingSavedSession = false;
        _cmRetryCount = 0;

        if (rememberSession)
        {
            SecureSessionStore.SaveSession(
                accountName,
                refreshToken,
                guardData
            );
        }
    }

    private void LogOnWithInMemorySession()
    {
        if (
            string.IsNullOrWhiteSpace(_sessionAccountName)
            || string.IsNullOrWhiteSpace(_sessionAccessToken)
        )
        {
            ErrorOccurred?.Invoke(
                "Steam login session is unavailable. Please authenticate again."
            );
            return;
        }

        _steamUser.LogOn(
            new SteamUser.LogOnDetails
            {
                Username = _sessionAccountName,
                AccessToken = _sessionAccessToken,
                ShouldRememberPassword = _rememberSession,
                LoginID = _loginId,
                MachineName = $"{Environment.MachineName} ({AppName})",
            }
        );
    }

    private async Task RetryAnotherCmAsync()
    {
        if (_cmRetryInProgress)
        {
            return;
        }

        if (_cmRetryCount >= MaxCmRetries)
        {
            ErrorOccurred?.Invoke(
                $"Steam requested another Connection Manager {MaxCmRetries} times. "
                + "Please wait a moment and reconnect manually."
            );
            Disconnect();
            return;
        }

        _cmRetryInProgress = true;
        _cmRetryCount++;

        StatusChanged?.Invoke(
            $"Steam requested another Connection Manager. "
            + $"Retrying automatically ({_cmRetryCount}/{MaxCmRetries})..."
        );

        try
        {
            // SteamKit already marks the CM that returned TryAnotherCM as bad.
            // Disconnect, wait briefly, then Connect() so another CM can be selected.
            _steamClient.Disconnect();

            await Task.Delay(900);

            if (_disposed)
            {
                return;
            }

            StatusChanged?.Invoke(
                $"Connecting to another Steam CM ({_cmRetryCount}/{MaxCmRetries})..."
            );

            _steamClient.Connect();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(
                $"Steam CM retry failed: {ex.GetType().Name}: {ex.Message}"
            );
        }
    }

    private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        if (callback.Result == EResult.TryAnotherCM)
        {
            _ = RetryAnotherCmAsync();
            return;
        }

        if (callback.Result != EResult.OK)
        {
            var invalidStoredToken =
                _usingSavedSession
                && callback.Result is
                    EResult.InvalidPassword
                    or EResult.InvalidSignature
                    or EResult.AccessDenied
                    or EResult.Expired
                    or EResult.Revoked;

            if (invalidStoredToken && !string.IsNullOrWhiteSpace(_sessionAccountName))
            {
                SecureSessionStore.DeleteSession(_sessionAccountName!);

                ErrorOccurred?.Invoke(
                    $"Saved session for '{_sessionAccountName}' was rejected by Steam "
                    + $"({callback.Result}). It has been removed; authenticate again."
                );
            }
            else
            {
                ErrorOccurred?.Invoke(
                    $"Steam logon failed: {callback.Result} / {callback.ExtendedResult}"
                );
            }

            Disconnect();
            return;
        }

        _cmRetryInProgress = false;
        _cmRetryCount = 0;
        _usingSavedSession = false;
        IsLoggedOn = true;
        var steamId = _steamClient.SteamID?.ConvertToUInt64().ToString() ?? "?";

        if (
            !_accountChangedSent
            && !string.IsNullOrWhiteSpace(_sessionAccountName)
        )
        {
            _accountChangedSent = true;
            AccountChanged?.Invoke(_sessionAccountName!);
        }

        LoginStateChanged?.Invoke(true);

        if (_autoReconnectInProgress && _idling)
        {
            RestoreIdleAfterReconnect();
            return;
        }

        AppLogger.Info($"Steam login successful for account {_sessionAccountName ?? "?"}.");

        StatusChanged?.Invoke(
            $"Logged in as {_sessionAccountName ?? "?"} | SteamID {steamId} | waiting for account library..."
        );
    }

    private void OnAccountInfo(SteamUser.AccountInfoCallback callback)
    {
        _accountInfoReceived = true;
        StatusChanged?.Invoke(
            IsLoggedOn
                ? "Steam account info ready."
                : "Steam account info received."
        );
    }

    private async void OnLicenseList(SteamApps.LicenseListCallback callback)
    {
        if (callback.Result != EResult.OK)
        {
            ErrorOccurred?.Invoke(
                $"Steam returned license list error: {callback.Result}"
            );
            return;
        }

        StatusChanged?.Invoke(
            $"Received {callback.LicenseList.Count} Steam licenses. Resolving library..."
        );

        try
        {
            await SyncOwnedLibraryAsync(callback.LicenseList);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(
                $"Library sync failed: {ex.GetType().Name}: {ex.Message}"
            );
        }
    }

    private async Task SyncOwnedLibraryAsync(
        IReadOnlyCollection<SteamApps.LicenseListCallback.License> licenses
    )
    {
        if (!await _librarySyncLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            var packageRequests = licenses
                .Where(l => l.PackageID > 0)
                .GroupBy(l => l.PackageID)
                .Select(g =>
                {
                    var license = g.First();
                    return new SteamApps.PICSRequest(
                        license.PackageID,
                        license.AccessToken
                    );
                })
                .ToList();

            var appIds = new HashSet<uint>();
            var packageProcessed = 0;

            foreach (var chunk in Chunk(packageRequests, 100))
            {
                var response = await _steamApps.PICSGetProductInfo(
                    Array.Empty<SteamApps.PICSRequest>(),
                    chunk
                );

                var packageResults = response.Results;

                if (packageResults is null)
                {
                    StatusChanged?.Invoke(
                        "Steam returned no package results for this PICS batch."
                    );
                    continue;
                }

                foreach (var result in packageResults)
                {
                    foreach (var package in result.Packages.Values)
                    {
                        var appIdNode = package.KeyValues["appids"];

                        foreach (var child in appIdNode.Children)
                        {
                            var id = child.AsUnsignedInteger();

                            if (id > 0)
                            {
                                appIds.Add(id);
                            }
                        }
                    }
                }

                packageProcessed += chunk.Count;
                StatusChanged?.Invoke(
                    $"Resolving packages {packageProcessed}/{packageRequests.Count} | {appIds.Count} AppIDs found..."
                );
            }

            var games = new Dictionary<uint, GameEntry>();
            var appList = appIds.OrderBy(id => id).ToList();
            var appProcessed = 0;

            foreach (var ids in Chunk(appList, 100))
            {
                var requests = ids
                    .Select(id => new SteamApps.PICSRequest(id))
                    .ToList();

                var response = await _steamApps.PICSGetProductInfo(
                    requests,
                    Array.Empty<SteamApps.PICSRequest>()
                );

                var appResults = response.Results;

                if (appResults is null)
                {
                    StatusChanged?.Invoke(
                        "Steam returned no app results for this PICS batch."
                    );
                    continue;
                }

                foreach (var result in appResults)
                {
                    foreach (var app in result.Apps.Values)
                    {
                        var common = app.KeyValues["common"];
                        var name = common["name"].AsString() ?? string.Empty;
                        var type = common["type"].AsString() ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(name))
                        {
                            continue;
                        }

                        if (!string.Equals(
                                type,
                                "game",
                                StringComparison.OrdinalIgnoreCase
                            ))
                        {
                            continue;
                        }

                        games[app.ID] = new GameEntry
                        {
                            AppId = app.ID,
                            Name = name,
                            Type = type,
                            Source = "Steam account",
                        };
                    }
                }

                appProcessed += ids.Count;
                StatusChanged?.Invoke(
                    $"Resolving game names {appProcessed}/{appList.Count} | {games.Count} games..."
                );
            }

            var finalGames = games.Values
                .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(_sessionAccountName))
            {
                LibraryCache.Save(_sessionAccountName!, finalGames);
            }
            LibraryUpdated?.Invoke(finalGames);

            StatusChanged?.Invoke(
                $"Library ready: {finalGames.Count} games found."
            );
        }
        finally
        {
            _librarySyncLock.Release();
        }
    }

    public async Task<GameEntry?> ResolveAppAsync(uint appId)
    {
        if (!IsLoggedOn)
        {
            return new GameEntry
            {
                AppId = appId,
                Name = $"App {appId}",
                Source = "Manual",
            };
        }

        try
        {
            var requests = new[]
            {
                new SteamApps.PICSRequest(appId),
            };

            var response = await _steamApps.PICSGetProductInfo(
                requests,
                Array.Empty<SteamApps.PICSRequest>()
            );

            var resolveResults = response.Results;

            if (resolveResults is null)
            {
                return new GameEntry
                {
                    AppId = appId,
                    Name = $"App {appId}",
                    Source = "Manual",
                };
            }

            foreach (var result in resolveResults)
            {
                if (!result.Apps.TryGetValue(appId, out var app))
                {
                    continue;
                }

                var common = app.KeyValues["common"];
                var name = common["name"].AsString() ?? string.Empty;
                var type = common["type"].AsString() ?? string.Empty;

                return new GameEntry
                {
                    AppId = appId,
                    Name = string.IsNullOrWhiteSpace(name)
                        ? $"App {appId}"
                        : name,
                    Type = string.IsNullOrWhiteSpace(type) ? "unknown" : type,
                    Source = "Manual",
                };
            }
        }
        catch
        {
            // Fall through to a generic manual entry.
        }

        return new GameEntry
        {
            AppId = appId,
            Name = $"App {appId}",
            Source = "Manual",
        };
    }

    public void StartIdle(
        IReadOnlyCollection<uint> appIds,
        bool publicPresence,
        uint? preferredPublicAppId
    )
    {
        if (!IsLoggedOn)
        {
            throw new InvalidOperationException("Steam is not logged in.");
        }

        var unique = appIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (unique.Count == 0)
        {
            throw new InvalidOperationException("Select at least one AppID.");
        }

        if (unique.Count > MaxIdleApps)
        {
            throw new InvalidOperationException(
                $"Steam idling is limited to {MaxIdleApps} AppIDs at once."
            );
        }

        if (_idling)
        {
            StopIdle();
        }

        var ordered = new List<uint>();

        if (
            publicPresence
            && preferredPublicAppId.HasValue
            && unique.Contains(preferredPublicAppId.Value)
        )
        {
            ordered.Add(preferredPublicAppId.Value);
        }

        ordered.AddRange(unique.Where(id => !ordered.Contains(id)));

        // Deterministic presence behavior:
        // OFF  -> Invisible
        // ON   -> Online
        // STOP -> Invisible
        //
        // We intentionally do not restore GetPersonaState(), because with
        // multiple Steam clients on the same account that cached state can
        // differ from the visibility the user expects.
        if (publicPresence)
        {
            if (!_accountInfoReceived)
            {
                StatusChanged?.Invoke(
                    "Account info not seen yet; forcing persona Online anyway."
                );
            }

            _steamFriends.SetPersonaState(EPersonaState.Online);

            StatusChanged?.Invoke(
                "Public presence ON: persona set to Online."
            );
        }
        else
        {
            _steamFriends.SetPersonaState(EPersonaState.Invisible);

            StatusChanged?.Invoke(
                "Public presence OFF: persona set to Invisible while idling."
            );
        }

        var playMessage =
            new ClientMsgProtobuf<CMsgClientGamesPlayed>(
                EMsg.ClientGamesPlayed
            );

        foreach (var appId in ordered)
        {
            playMessage.Body.games_played.Add(
                new CMsgClientGamesPlayed.GamePlayed
                {
                    game_id = new GameID(appId),
                }
            );
        }

        _steamClient.Send(playMessage);

        _activeAppIds = ordered;
        _idlePublicPresence = publicPresence;
        _idlePreferredPublicAppId = preferredPublicAppId;
        _idling = true;

        AppLogger.Info(
            $"Idle started | count={ordered.Count} | "
            + $"presence={(publicPresence ? "public" : "invisible")} | "
            + $"AppIDs={string.Join(",", ordered)}"
        );

        StatusChanged?.Invoke(
            $"Idling {ordered.Count} game(s): {string.Join(", ", ordered)}"
        );
        IdleStateChanged?.Invoke(true);
    }

    public void StopIdle()
    {
        if (!_idling)
        {
            return;
        }

        // User intent wins over automatic recovery. Once STOP is requested,
        // never restore these AppIDs after a reconnect.
        CancelAutoReconnect();

        if (IsLoggedOn)
        {
            try
            {
                var stopMessage =
                    new ClientMsgProtobuf<CMsgClientGamesPlayed>(
                        EMsg.ClientGamesPlayed
                    );

                _steamClient.Send(stopMessage);
            }
            catch
            {
                // The connection may have vanished between the IsLoggedOn
                // check and the send. Local state is still cleared below.
            }

            try
            {
                _steamFriends.SetPersonaState(EPersonaState.Invisible);
            }
            catch
            {
                // Best effort. The account may already be disconnected.
            }
        }

        _activeAppIds.Clear();
        _idlePublicPresence = false;
        _idlePreferredPublicAppId = null;
        _idling = false;

        AppLogger.Info("Idle stopped.");

        StatusChanged?.Invoke(
            IsLoggedOn
                ? "Idle stopped. All games cleared and persona set to Invisible."
                : "Idle stopped locally. No games will be restored after reconnect."
        );

        IdleStateChanged?.Invoke(false);
    }

    private void OnPlayingSessionState(
        SteamUser.PlayingSessionStateCallback callback
    )
    {
        if (_idling)
        {
            StatusChanged?.Invoke(
                $"Steam confirmed playing-session state | {_activeAppIds.Count} active AppID(s)."
            );
        }
    }

    private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
    {
        IsLoggedOn = false;

        StatusChanged?.Invoke($"Logged off Steam: {callback.Result}");
        LoginStateChanged?.Invoke(false);

        if (!_idling)
        {
            _activeAppIds.Clear();
            IdleStateChanged?.Invoke(false);
        }
    }

    private void OnDisconnected(SteamClient.DisconnectedCallback callback)
    {
        IsConnected = false;
        IsLoggedOn = false;

        if (_cmRetryInProgress)
        {
            StatusChanged?.Invoke(
                $"Switching Steam Connection Manager "
                + $"({_cmRetryCount}/{MaxCmRetries})..."
            );
            return;
        }

        LoginStateChanged?.Invoke(false);

        var canRestoreActiveIdle =
            _idling
            && !callback.UserInitiated
            && !string.IsNullOrWhiteSpace(_sessionAccountName)
            && !string.IsNullOrWhiteSpace(_sessionAccessToken);

        if (canRestoreActiveIdle)
        {
            StatusChanged?.Invoke(
                $"Steam connection lost. Preserving {_activeAppIds.Count} active AppID(s) "
                + "and starting automatic recovery..."
            );

            StartAutoReconnect();
            return;
        }

        if (!_idling)
        {
            _activeAppIds.Clear();
        }

        StatusChanged?.Invoke(
            callback.UserInitiated
                ? "Disconnected from Steam."
                : "Steam connection lost."
        );

        if (_idling)
        {
            // We could not safely reconnect (for example, no in-memory token).
            _activeAppIds.Clear();
            _idlePublicPresence = false;
            _idlePreferredPublicAppId = null;
            _idling = false;
            IdleStateChanged?.Invoke(false);
        }
    }

    private void StartAutoReconnect()
    {
        if (_autoReconnectInProgress || !_idling)
        {
            return;
        }

        _autoReconnectCts?.Dispose();
        _autoReconnectCts = new CancellationTokenSource();

        _autoReconnectInProgress = true;
        _autoReconnectAttempt = 0;

        ReconnectStateChanged?.Invoke(true, 0);

        _ = AutoReconnectLoopAsync(_autoReconnectCts.Token);
    }

    private async Task AutoReconnectLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (
                !cancellationToken.IsCancellationRequested
                && _idling
                && !IsLoggedOn
                && _autoReconnectAttempt < MaxAutoReconnectAttempts
            )
            {
                _autoReconnectAttempt++;

                var delaySeconds = AutoReconnectDelaysSeconds[
                    Math.Min(
                        _autoReconnectAttempt - 1,
                        AutoReconnectDelaysSeconds.Length - 1
                    )
                ];

                ReconnectStateChanged?.Invoke(
                    true,
                    _autoReconnectAttempt
                );

                StatusChanged?.Invoke(
                    $"Reconnect attempt {_autoReconnectAttempt}/{MaxAutoReconnectAttempts} "
                    + $"in {delaySeconds}s..."
                );

                await Task.Delay(
                    TimeSpan.FromSeconds(delaySeconds),
                    cancellationToken
                );

                if (
                    cancellationToken.IsCancellationRequested
                    || !_idling
                    || IsLoggedOn
                )
                {
                    break;
                }

                try
                {
                    if (IsConnected)
                    {
                        _steamClient.Disconnect();
                        await Task.Delay(300, cancellationToken);
                    }

                    StatusChanged?.Invoke(
                        $"Reconnect attempt {_autoReconnectAttempt}/{MaxAutoReconnectAttempts}: "
                        + "connecting to Steam..."
                    );

                    _steamClient.Connect();
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke(
                        $"Reconnect attempt {_autoReconnectAttempt} could not start: {ex.Message}"
                    );
                }

                // Give Steam/SteamKit enough time to connect and log on.
                for (var i = 0; i < 72; i++)
                {
                    if (
                        cancellationToken.IsCancellationRequested
                        || !_idling
                        || IsLoggedOn
                    )
                    {
                        break;
                    }

                    await Task.Delay(250, cancellationToken);
                }

                if (IsLoggedOn)
                {
                    break;
                }

                try
                {
                    _steamClient.Disconnect();
                }
                catch
                {
                    // Best effort before the next attempt.
                }
            }

            if (
                !cancellationToken.IsCancellationRequested
                && _idling
                && !IsLoggedOn
            )
            {
                _autoReconnectInProgress = false;

                ReconnectStateChanged?.Invoke(
                    false,
                    _autoReconnectAttempt
                );

                StatusChanged?.Invoke(
                    $"Automatic reconnect failed after {_autoReconnectAttempt} attempt(s). "
                    + "Idle is paused; press STOP ALL or reconnect manually."
                );
            }
        }
        catch (OperationCanceledException)
        {
            // Normal when STOP ALL, manual Disconnect, or successful recovery wins.
        }
    }

    private void RestoreIdleAfterReconnect()
    {
        if (!_idling || _activeAppIds.Count == 0 || !IsLoggedOn)
        {
            CancelAutoReconnect();
            return;
        }

        try
        {
            _steamFriends.SetPersonaState(
                _idlePublicPresence
                    ? EPersonaState.Online
                    : EPersonaState.Invisible
            );

            var playMessage =
                new ClientMsgProtobuf<CMsgClientGamesPlayed>(
                    EMsg.ClientGamesPlayed
                );

            foreach (var appId in _activeAppIds)
            {
                playMessage.Body.games_played.Add(
                    new CMsgClientGamesPlayed.GamePlayed
                    {
                        game_id = new GameID(appId),
                    }
                );
            }

            _steamClient.Send(playMessage);

            var restoredCount = _activeAppIds.Count;
            var visibility = _idlePublicPresence
                ? "Online/public"
                : "Invisible/private";

            CancelAutoReconnect();

            StatusChanged?.Invoke(
                $"Steam reconnected. Restored {restoredCount} AppID(s) "
                + $"with {visibility} presence."
            );

            IdleStateChanged?.Invoke(true);
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(
                $"Steam reconnected, but idle restore failed: {ex.Message}"
            );
        }
    }

    private void CancelAutoReconnect()
    {
        try
        {
            _autoReconnectCts?.Cancel();
        }
        catch
        {
            // Best effort.
        }

        _autoReconnectCts?.Dispose();
        _autoReconnectCts = null;

        var wasReconnecting = _autoReconnectInProgress;

        _autoReconnectInProgress = false;
        _autoReconnectAttempt = 0;

        if (wasReconnecting)
        {
            ReconnectStateChanged?.Invoke(false, 0);
        }
    }

    public void Disconnect()
    {
        CancelAutoReconnect();

        _cmRetryInProgress = false;
        _cmRetryCount = 0;

        try
        {
            StopIdle();

            if (IsLoggedOn)
            {
                try
                {
                    _steamFriends.SetPersonaState(EPersonaState.Invisible);
                }
                catch
                {
                    // Best effort.
                }
            }
        }
        catch
        {
            // Best-effort shutdown.
        }

        // Important for persistent sessions:
        // Disconnect the transport without SteamUser.LogOff(). Steam LogOff can
        // revoke refresh tokens; a normal app close/disconnect should keep a
        // remembered session usable for the next launch.
        try
        {
            _steamClient.Disconnect();
        }
        catch
        {
            // Best-effort shutdown.
        }

        IsConnected = false;
        IsLoggedOn = false;
        _pendingLogin = null;
        _sessionAccountName = null;
        _sessionAccessToken = null;
        _rememberSession = false;
        _usingSavedSession = false;
        _accountChangedSent = false;
        _idlePublicPresence = false;
        _idlePreferredPublicAppId = null;

        LoginStateChanged?.Invoke(false);
    }

    private static List<List<T>> Chunk<T>(
        IReadOnlyList<T> items,
        int size
    )
    {
        var result = new List<List<T>>();

        for (var i = 0; i < items.Count; i += size)
        {
            var count = Math.Min(size, items.Count - i);
            var chunk = new List<T>(count);

            for (var j = 0; j < count; j++)
            {
                chunk.Add(items[i + j]);
            }

            result.Add(chunk);
        }

        return result;
    }

    private static byte[] BuildQrPng(string challengeUrl)
    {
        using var qrGenerator = new QRCodeGenerator();

        using var qrData = qrGenerator.CreateQrCode(
            challengeUrl,
            QRCodeGenerator.ECCLevel.M
        );

        using var qr = new PngByteQRCode(qrData);

        // Render a real square QR image instead of terminal/ASCII blocks.
        // The quiet zone is preserved so Steam's mobile scanner can detect it.
        return qr.GetGraphic(
            pixelsPerModule: 12,
            drawQuietZones: true
        );
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        Disconnect();

        _callbackCts.Cancel();
        _callbackCts.Dispose();
        _librarySyncLock.Dispose();
    }
}
