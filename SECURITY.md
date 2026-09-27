# Security

## Never commit Steam credentials or authenticator secrets

Steam Idle Manager does not require credentials to be stored in the repository.

Do **not** commit or attach any of the following to GitHub issues, pull requests,
or source-control history:

- Steam passwords
- refresh/access tokens
- `accounts.json`
- Steam Desktop Authenticator `.maFile` files
- `shared_secret`
- `identity_secret`
- `.env` files containing credentials
- screenshots or logs that contain authentication material

Saved sessions created by Steam Idle Manager are stored outside the repository
under the current Windows user's local application data directory and are
protected with Windows DPAPI.

## If a secret is accidentally published

Remove it from the repository/history where appropriate and revoke or rotate the
affected Steam credential/session immediately.

## Diagnostic logs

Logs are stored under:

`%LOCALAPPDATA%\SteamIdleManager\logs\`

Before posting a log publicly, review it for account-specific information.
