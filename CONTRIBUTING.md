# Contributing to Steam Idle Manager

Thanks for your interest in improving Steam Idle Manager.

## Before opening a pull request

- Keep changes focused and easy to review.
- Do not commit Steam credentials, refresh tokens, `.maFile` files, or other authentication secrets.
- Test authentication, idling, STOP ALL, presence behavior, and reconnect logic when your change touches the Steam session core.
- Preserve the privacy-first behavior: STOP ALL returns the Steam persona to Invisible.
- Run `RUN_DEV.bat` and make sure the project builds successfully.

## Development environment

- Windows 10/11
- .NET 8 SDK
- Visual Studio, VS Code, Rider, or another C# editor

Start the development build with:

```bat
RUN_DEV.bat
```

## Release build

The supported distribution build is:

```bat
BUILD_RELEASE.bat
```

## Reporting bugs

Please include:

- Steam Idle Manager version
- Windows version
- steps to reproduce
- expected behavior
- actual behavior
- relevant diagnostic log excerpts

Review logs before posting them publicly and remove any account-specific or authentication-related information.

## Feature requests

Explain the problem you are trying to solve and why the feature would improve Steam Idle Manager.

## License

By contributing, you agree that your contributions will be licensed under the project's MIT License.
Third-party dependencies remain under their respective licenses.
