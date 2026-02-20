# Beamer Viewer

Windows desktop viewer built with WPF (.NET 8) and WebView2.
This version renders posts in a native local UI and polls the Beamer API directly (no embedded widget).

## Requirements

- .NET SDK 8+
- NuGet access

## Build (Debug)

```bash
dotnet build Beamer_viewer.csproj -nologo
```

## Publish Windows EXE (Release)

```bash
dotnet publish Beamer_viewer.csproj -c Release -r win-x64 --self-contained true -nologo
```

Published output:

`bin/Release/net8.0-windows/win-x64/publish/Beamer_viewer.exe`

## Run on Windows

Copy the `publish` folder to a Windows machine and run:

`Beamer_viewer.exe`

## Configure API Access

Edit:

`Beamerviewer.config.json`

Required field:

- `ApiKey`: your private Beamer API key

Useful fields:

- `RefreshMs`: polling interval in milliseconds (minimum 1000)
- `MaxPosts`: how many posts to show
- `ApiBaseUrl`: defaults to `https://api.getbeamer.com/v0`
- `RequestTimeoutMs`: API timeout in milliseconds
- `ViewerName`: optional override for Beamer viewer name (defaults to computer name)
- `ViewerUserId`: optional override for Beamer user id (defaults to OS account name)
- `ViewerEmail`: optional viewer email
- `EnableViewTracking`: set `true` to send Beamer post view tracking from this app when a post is marked as read in the UI

Message behavior:

- Posts are polled from the API for display.
- Beamer views are tracked when you click `Mark as read` on a post.
- Read posts are hidden locally in the app.

When `ApiKey` is missing or invalid, the app shows a configuration/error state in the UI with the active config file path.

## Notes

- App manifest: `app.manifest`
- Config file template: `Beamerviewer.config.json`
