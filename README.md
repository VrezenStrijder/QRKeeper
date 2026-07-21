# QRKeeper  
[中文](./README_cn.md)

QRKeeper is a cross-platform QR code collection and management tool for Windows desktop and Android. It stores QR content together with generated QR images and supports scanning, image import, backup/restore, LAN sync, and online update checks.  

## Features

- QR record management: create, search, edit title/note, delete, and drag-sort records.
- QR image persistence: preview, export, save, and share generated QR images.
- Multiple import paths: manual content entry, image import, Android camera scanning, and Windows screen-region recognition.
- Backup and restore: compatible `.qrbak` backup files across desktop and Android.
- Import preview: inspect backup contents before importing selected records.
- LAN sync: QRKeeper clients on the same local network can discover each other and merge missing records by exact title + content matching.
- Localization: Chinese and English, with initial language selected from the operating system language.
- Theme and color styles: system/light/dark theme modes and selectable color palettes.
- Online updates: checks GitHub Releases plus `update.json` for Windows and Android packages.

## Platforms

- Windows desktop: Avalonia + FluentAvalonia.
- Android: Avalonia Android, with ARM64 release builds for real devices.

## Getting Started

Install the .NET SDK, Android workload, and Microsoft JDK 17. Android debugging can be done from Visual Studio or the command line.

```powershell
dotnet build src\QRKeeper.sln -c Debug
dotnet run --project src\QRKeeper.UI\QRKeeper.UI.csproj -c Debug
dotnet build src\QRKeeper.Android\QRKeeper.Android.csproj -f net8.0-android -c Debug
```

Build an Android Release APK:

```powershell
.\scripts\build-android-release.ps1
```

## Online Updates

An manifest is available at `release/update.json`. The app checks the latest Release, reads that manifest, and opens the matching platform download link. Silent installation is not implemented.

## Data And Sync Notes

- Sync is additive only. It does not overwrite or delete remote records.
- Duplicate records are matched by exact QR title and QR content.
- Restore replaces local data, so the app creates a safety backup before restore.
- LAN sync depends on same Wi-Fi/LAN access, permissions, firewall rules, and router broadcast settings.

