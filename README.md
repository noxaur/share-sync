# Jellyfin SyncPlay Share

Adds a **Share** action to Jellyfin's built-in SyncPlay menu. The action copies a share URL for the current SyncPlay group. Existing Jellyfin users who open that URL are taken to the web app and joined to the group if their account has SyncPlay and media access.

This plugin targets Jellyfin `10.11.x` and uses [File Transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation) to inject its client script into served `jellyfin-web` content without modifying files on disk.

## Requirements

- Jellyfin `10.11.x`
- .NET SDK 9.0 to build
- File Transformation plugin installed in Jellyfin

## Install Test Build

Add this plugin repository URL in Jellyfin:

```text
https://raw.githubusercontent.com/noxaur/share-sync/main/manifest.json
```

Install **File Transformation** first, then install **SyncPlay Share** from the catalog and restart Jellyfin.

## Build

```sh
dotnet build Jellyfin.Plugin.SyncPlayShare.sln /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary
```

The debug plugin DLL is emitted at:

```text
Jellyfin.Plugin.SyncPlayShare/bin/Debug/net9.0/Jellyfin.Plugin.SyncPlayShare.dll
```

The test catalog serves:

```text
dist/Jellyfin.Plugin.SyncPlayShare_10.11.0_1.0.1.0.zip
```

## Test

```sh
node scripts/syncplay-share.selftest.js
dotnet build Jellyfin.Plugin.SyncPlayShare.sln /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary
```

## Configuration

- `Enabled`: injects the Share action when true.
- `LogLevel`: `Error`, `Info`, `Debug`, or `Verbose`.
- `ClientConsoleLogging`: writes debug and verbose browser logs when enabled.
- `CopyToastEnabled`: shows copy/join status toasts.
- `ShareButtonLabel`: defaults to `Share`.

Errors are always logged with the `[SyncPlayShare]` prefix.
