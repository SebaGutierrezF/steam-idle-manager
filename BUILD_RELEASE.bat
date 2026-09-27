@echo off
setlocal EnableExtensions
title Build Steam Idle Manager Release
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: .NET SDK was not found.
    pause
    exit /b 1
)

where powershell >nul 2>nul
if errorlevel 1 (
    echo ERROR: PowerShell was not found.
    pause
    exit /b 1
)

for /f "usebackq delims=" %%V in (`powershell -NoProfile -Command "$m = Select-String -Path '%~dp0SteamIdleManager.csproj' -Pattern '<Version>([^<]+)</Version>'; if ($m) { $m.Matches[0].Groups[1].Value }"`) do (
    set "APP_VERSION=%%V"
)

if not defined APP_VERSION (
    echo ERROR: Could not read the application version from SteamIdleManager.csproj.
    pause
    exit /b 1
)

set "RELEASE_ROOT=%~dp0release"
set "RELEASE_DIR=%RELEASE_ROOT%\SteamIdleManager-v%APP_VERSION%"
set "RELEASE_ZIP=%RELEASE_ROOT%\SteamIdleManager-v%APP_VERSION%-win-x64.zip"

echo ================================================================
echo Steam Idle Manager v%APP_VERSION% - FINAL RELEASE BUILD
echo ================================================================
echo.

if exist "%RELEASE_DIR%" rmdir /S /Q "%RELEASE_DIR%"
if exist "%RELEASE_ZIP%" del /Q "%RELEASE_ZIP%"
mkdir "%RELEASE_DIR%" >nul 2>nul

REM dotnet publish performs the correct runtime-specific restore for win-x64.
dotnet publish -c Release -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:PublishReadyToRun=true ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "%RELEASE_DIR%"

if errorlevel 1 (
    echo.
    echo ERROR: release publish failed.
    pause
    exit /b 1
)

if not exist "%RELEASE_DIR%\SteamIdleManager.exe" (
    echo.
    echo ERROR: final SteamIdleManager.exe is missing.
    pause
    exit /b 1
)

del /Q "%RELEASE_DIR%\*.pdb" >nul 2>nul

copy /Y "%~dp0README_RELEASE.txt" "%RELEASE_DIR%\README.txt" >nul
copy /Y "%~dp0RELEASE-NOTES.txt" "%RELEASE_DIR%\RELEASE-NOTES.txt" >nul
copy /Y "%~dp0THIRD-PARTY-NOTICES.txt" "%RELEASE_DIR%\THIRD-PARTY-NOTICES.txt" >nul

copy /Y "%RELEASE_DIR%\SteamIdleManager.exe" "%~dp0SteamIdleManager.exe" >nul

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Compress-Archive -Path '%RELEASE_DIR%\*' -DestinationPath '%RELEASE_ZIP%' -Force"

if errorlevel 1 (
    echo.
    echo ERROR: ZIP creation failed.
    echo The release folder itself is still valid:
    echo %RELEASE_DIR%
    pause
    exit /b 1
)

echo.
echo ================================================================
echo RELEASE BUILD COMPLETED
echo.
echo Version:
echo %APP_VERSION%
echo.
echo Release folder:
echo %RELEASE_DIR%
echo.
echo Distribution ZIP:
echo %RELEASE_ZIP%
echo.
echo Convenience EXE:
echo %~dp0SteamIdleManager.exe
echo ================================================================
echo.
pause
