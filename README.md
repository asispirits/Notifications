# Spirits Notifications

Windows desktop viewer built with WPF (.NET 8) and WebView2.
This version renders posts in a native local UI and polls the Beamer API directly (no embedded widget).

## Requirements

- .NET SDK 8+
- NuGet access

## Build (Debug)

```bash
dotnet build SpiritsNotifications.csproj -nologo
```

## Publish Windows EXE (Release)

```bash
dotnet publish SpiritsNotifications.csproj -c Release -r win-x64 --self-contained true -nologo
```

Published output:

`bin/Release/net8.0-windows/win-x64/publish/SpiritsNotifications.exe`

## Run on Windows

Copy the `publish` folder to a Windows machine and run:

`SpiritsNotifications.exe`

## Configure API Access

Edit:

`SpiritsNotifications.config.json`

Required field:

- `ApiKey`: your private Beamer API key

Useful fields:

- `RefreshMs`: polling interval in milliseconds (minimum 1000)
- `MaxPosts`: how many posts to show
- `ApiBaseUrl`: defaults to `https://api.getbeamer.com/v0`
- `RequestTimeoutMs`: API timeout in milliseconds
- `ViewerName`: optional override for Beamer viewer name (defaults to `Cname` from `C:\TEMP\cheader.dbf`)
- `ViewerUserId`: optional override for Beamer user id (defaults to `Cthisreg` from `C:\TEMP\cheader.dbf`)
- `ViewerEmail`: optional viewer email
- `EnableViewTracking`: set `true` to send Beamer post view tracking from this app when a post is marked as read in the UI
- `ArchiveMessages`: set `true` to enable local archive storage and the `ARCHIVED MESSAGES` UI button

Message behavior:

- Posts are polled from the API for display.
- Beamer views are tracked once when you click `OK` on a post.
- If `ArchiveMessages=true`, each newly acknowledged post is saved to `MESSAGE_ARCHIVE`, removed from the live list, and shown only in `ARCHIVED MESSAGES`.
- If `ArchiveMessages=false`, no archive folder is created, no local archive files are written, and acknowledged posts stay in the live list grayed out.

When `ApiKey` is missing or invalid, the app shows a configuration/error state in the UI with the active config file path.

## Notes

- App manifest: `app.manifest`
- Config file template: `SpiritsNotifications.config.json`
