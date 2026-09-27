Steam Idle Manager v1.0.2
==========================

Windows x64

Steam Idle Manager is an independent Windows utility for managing Steam idle
sessions for selected AppIDs.

MAIN FEATURES
-------------
- 1 to 32 simultaneous Steam AppIDs
- Fixed-duration or indefinite idle
- Public presence OFF: Invisible
- Public presence ON: Online + preferred public game
- STOP ALL clears the active game state and returns to Invisible
- QR login
- Username/password + Steam Guard code
- Steam Desktop Authenticator-friendly code entry
- Steam Guard email code support
- DPAPI-protected saved Steam sessions
- Multiple saved accounts
- Per-account Steam library cache
- Profiles, favorites and recent games
- Active-session panel
- Automatic reconnect while idling
- Restore AppIDs/presence after reconnect
- Minimize to system tray
- Auto-connect saved account
- Auto-start saved idle profile
- Start minimized
- Optional Start with Windows

QUICK START
-----------
1. Run SteamIdleManager.exe.
2. Click "Connect Steam".
3. Choose QR, Username / Password, or Saved account.
4. Select one or more games.
5. Choose duration and public-presence behavior.
6. Click START IDLE.
7. Use STOP ALL when finished.

SAVED AUTHENTICATION
--------------------
Passwords are never stored by Steam Idle Manager.

Remembered Steam refresh tokens and Steam Guard data are encrypted using
Windows DPAPI with CurrentUser scope. They are stored under:

%LOCALAPPDATA%\SteamIdleManager\

LOGS
----
Diagnostic logs are stored under:

%LOCALAPPDATA%\SteamIdleManager\logs\

Use About -> Open Logs Folder to access them.

IMPORTANT PRESENCE BEHAVIOR
---------------------------
Show as playing publicly OFF:
- persona is set to Invisible while idling.

Show as playing publicly ON:
- persona is set to Online;
- the preferred public game is sent first.

STOP ALL:
- clears all active AppIDs;
- returns the persona to Invisible.

NOTES
-----
- This is an independent utility.
- It is not affiliated with, endorsed by, or sponsored by Valve Corporation or Steam.
- Steam behavior and APIs can change over time.
- Use only accounts and AppIDs you are authorized to access.

See THIRD-PARTY-NOTICES.txt for dependency information.


LICENSE
-------
Steam Idle Manager's own source code is licensed under the MIT License.
Third-party dependencies retain their respective licenses.

Project repository:
https://github.com/SebaGutierrezF/steam-idle-manager

Support:
https://www.buymeacoffee.com/SebaGutierrezF
