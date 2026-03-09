# Notifications Widget Demo

This project is a widget-based demo variant of Notifications.

It uses the Beamer embed widget inside the app WebView2 UI and does not use the app's internal API polling flow.

## Project Copy

This demo lives in a separate folder:

`<parent-folder>/Notifications_widget`

Your original project remains unchanged in:

`<parent-folder>/Notifications`

## Beamer Embed

The embedded page uses this equivalent Beamer setup:

```html
<script>
  var beamer_config = {
    product_id: 'vEjlRlWp82033',
    user_id: "user's unique id"
  };
</script>
<script type="text/javascript" src="https://app.getbeamer.com/js/beamer-embed.js" defer="defer"></script>
```

In this app, `product_id` and user identity are injected from `Notifications.config.json`.

## Config

Edit:

`Notifications.config.json`

Required fields for widget mode:

- `ProductId`
- `UserId` (stable endpoint/user identifier)

Common optional fields:

- `ViewerUserId`, `ViewerName`, `ViewerEmail`
- `AutoOpenOnLaunch` (auto-open widget panel)
- `WidgetRefreshMs` (widget refresh cycle, minimum 30000)
- `Ui.AppTitle`

Segmentation fields:

- `SegmentFilters` (semicolon-delimited segments)
- `IncludeViewerUserIdSegment` (adds `UserId` as an extra segment)
- `SegmentMultiUser` (keeps Beamer per-user cookie separation)
- `SegmentForceFilter` (optional advanced Beamer filter override)
- `SegmentRole`, `SegmentFilter` (legacy compatibility)

## Segmentation

Use the single-step guide:

- `docs/SEGMENTATION_GUIDE.md`

This guide contains:

- endpoint setup
- first-launch automatic segmentation bootstrap
- `.bat` one-step command (`Configure-EndpointSegmentation.bat`)
- `.bat` wrapper commands (`Generate-SegmentFilters.bat`, `Apply-EndpointConfig.bat`)
- Beamer targeting steps
- validation and troubleshooting

## Build

```bash
cd <repo-root>
dotnet build Notifications.csproj -c Release -nologo
```

## Publish

```bash
cd <repo-root>
dotnet publish Notifications.csproj -c Release -r win-x64 --self-contained true -nologo
```

Published executable:

`bin/Release/net8.0-windows/win-x64/publish/Notifications.exe`

## Watchdog Scheduled Task

To auto-launch the app during the day when it is not running:

- `EnableLaunchWatchdogTask`: `true`
- `LaunchWatchdogTaskName`: task name
- `LaunchWatchdogIntervalMinutes`: run cadence (5-240)

Notes:

- The task is created with `/RL HIGHEST` (highest privileges).
- Run the app once as Administrator to register/update the task.
- Task action only starts `Notifications.exe` when the process is not already running.

Manual admin setup script (recommended if auto-setup is blocked):

```powershell
# Run PowerShell as Administrator
cd <repo-root>\scripts
.\Register-WatchdogTask.ps1 -ExePath "<publish-folder>\Notifications.exe" -TaskName "Notifications Widget Watchdog" -IntervalMinutes 30
```

Quick verify on Windows:

```cmd
schtasks /Query /TN "Notifications Widget Watchdog" /FO LIST
```
