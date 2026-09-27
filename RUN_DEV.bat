@echo off
setlocal
title Steam Idle Manager - Development Launcher
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo.
    echo ERROR: .NET SDK was not found.
    echo Install the .NET 8 SDK or newer, then try again.
    echo.
    pause
    exit /b 1
)

echo ================================================================
echo Steam Idle Manager - Development Launcher
echo ================================================================
echo.

dotnet restore
if errorlevel 1 (
    echo.
    echo ERROR: dotnet restore failed.
    echo.
    pause
    exit /b 1
)

dotnet build -c Release --no-restore
if errorlevel 1 (
    echo.
    echo ERROR: build failed.
    echo.
    pause
    exit /b 1
)

set "APP_EXE=%~dp0bin\Release\net8.0-windows\SteamIdleManager.exe"

if not exist "%APP_EXE%" (
    echo.
    echo ERROR: Build succeeded but SteamIdleManager.exe was not found:
    echo %APP_EXE%
    echo.
    pause
    exit /b 1
)

start "" "%APP_EXE%"
exit /b 0
