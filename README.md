# Aura Timer

**Aura Timer** is a clean, modern timer widget for Windows 11 that lets you run multiple countdowns at once. When a timer finishes, Aura Timer plays a chime, shows an in-app alert, and attempts to send a Windows notification.

## Features

- **Multiple timers**: keep several countdowns running side by side.
- **Quick presets**: 2, 5, 10, 15, 20, 25, 30, 45 minutes, and 1 hour.
- **Custom countdowns**: set your own hours, minutes, and seconds with an optional label.
- **Completion alerts**: sound + in-app notification when the set time is reached (plus a toast notification when supported).
- **Modern UI**: compact, dark, Windows 11-friendly widget design.

## Project structure

- `src/AuraTimer/` – WPF (.NET 8) application source
- `src/AuraTimer/Assets/` – app icon and chime sound
- `.github/workflows/build.yml` – GitHub Actions workflow to build and publish

## Run locally

### Prerequisites
- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (includes Windows desktop support)

### Build & run
```powershell
dotnet restore src/AuraTimer/AuraTimer.csproj
dotnet run --project src/AuraTimer/AuraTimer.csproj
```

## Build with GitHub Actions

This repo includes a workflow at `.github/workflows/build.yml` that builds and uploads **two** `win-x64` variants:
1. **`AuraTimer-win-x64-framework-dependent`** (`--self-contained false`) – smaller download, requires the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) on the target machine.
2. **`AuraTimer-win-x64-self-contained`** (`--self-contained true`) – includes the runtime, no .NET installation required (larger download).

To get the app:
1. Push this code to GitHub (or run the workflow manually).
2. Open the **Actions** tab and open the latest successful **build** run.
3. Download the artifact variant you prefer from the run artifacts.

## App icon and name

- **App name:** Aura Timer
- **Icon:** included as `src/AuraTimer/Assets/app.ico` (with SVG/PNG sources in the same folder)

## Notes

- Keep the app open while timers are running; closing the window stops active countdowns.
- Toast notifications depend on Windows notification settings and environment support; the chime and in-app alert provide reliable completion feedback.
- This was built locally without the .NET SDK available in the environment, so I couldn't compile it here—GitHub Actions (or a local machine with the .NET 8 SDK) should be used to verify/build. If restore/build surfaces any package or TFM issues, let me know and I can adjust.