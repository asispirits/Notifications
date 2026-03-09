# Configuration Reference

File name:

`Notifications.config.json`

## 1. File Resolution Order

At startup, the app resolves config in this order:

1. `AppContext.BaseDirectory/Notifications.config.json`
2. Legacy in base dir: `Beamerviewer.config.json` (auto-migrated)
3. Embedded template extraction to base directory
4. `%AppData%/Notifications/Notifications.config.json`
5. `%AppData%/Notifications/Beamerviewer.config.json` (auto-migrated)
6. `%AppData%/Beamer_viewer/Beamerviewer.config.json` (auto-migrated)
7. Embedded template extraction to `%AppData%/Notifications/Notifications.config.json`

## 2. Widget Build Example

```json
{
  "ProductId": "vEjlRlWp82033",
  "UserId": "store-104-pos-01",
  "ViewerUserId": "",
  "ViewerName": "",
  "ViewerEmail": "",
  "WidgetWidth": 560,
  "AutoOpenOnLaunch": true,
  "WidgetRefreshMs": 30000,
  "SegmentFilters": "tenant:asi;brand:spirits;region:east;site:store-104;role:cashier;env:prod",
  "SegmentRole": "",
  "SegmentFilter": "",
  "SegmentForceFilter": "",
  "SegmentMultiUser": true,
  "IncludeViewerUserIdSegment": false,
  "EnableLaunchWatchdogTask": true,
  "LaunchWatchdogTaskName": "Notifications Widget Watchdog",
  "LaunchWatchdogIntervalMinutes": 30,
  "Ui": {
    "EnableSecretMenu": false,
    "AppTitle": "Notifications Widget Demo"
  }
}
```

## 3. Core Widget Fields

| Field | Type | Default | Notes |
|---|---|---:|---|
| `ProductId` | string | `vEjlRlWp82033` | Beamer product ID. |
| `UserId` | string | `""` | Stable endpoint identity. If blank, app falls back to `ViewerUserId`/`cheader`/OS user. |
| `ViewerUserId` | string | `""` | Optional explicit viewer user id fallback. |
| `ViewerName` | string | `""` | Optional explicit display name fallback. |
| `ViewerEmail` | string | `""` | Optional email passed to Beamer. |
| `WidgetWidth` | int | `520` | Window width; minimum 320. |
| `AutoOpenOnLaunch` | bool | `true` | Open widget automatically at startup. |
| `WidgetRefreshMs` | long | `30000` | Soft refresh cadence; clamped `30000..3600000`. |

## 4. Segmentation Fields

| Field | Type | Default | Notes |
|---|---|---:|---|
| `SegmentFilters` | string | `""` | Primary segmentation string. Use semicolon-delimited tags. |
| `IncludeViewerUserIdSegment` | bool | `false` | Appends viewer `UserId` as a segment token. |
| `SegmentMultiUser` | bool | `true` | Enables per-user Beamer cookie scoping. |
| `SegmentForceFilter` | string | `""` | Optional Beamer `force_filter` override. |
| `SegmentRole` | string | `""` | Legacy compatibility. Included if non-empty. |
| `SegmentFilter` | string | `""` | Legacy compatibility. Included if non-empty. |

### 4.1 Segment Resolution Priority

The app builds final segment filters in this order:

1. `SegmentFilters` (all tokens)
2. `SegmentRole` (if set)
3. `SegmentFilter` (if set)
4. Viewer `UserId` (if `IncludeViewerUserIdSegment=true`)

Duplicates are removed case-insensitively.

### 4.2 Segment Format

- Use lowercase tags
- Prefer `key:value` style
- Separate tags with `;`

Example:

`tenant:asi;brand:spirits;region:east;site:store-104;role:cashier;env:prod`

## 5. Watchdog Scheduled Task Fields

| Field | Type | Default | Notes |
|---|---|---:|---|
| `EnableLaunchWatchdogTask` | bool | `false` | When true, app attempts to create/update watchdog task. |
| `LaunchWatchdogTaskName` | string | `Notifications Widget Watchdog` | Trimmed; max 120 chars. |
| `LaunchWatchdogIntervalMinutes` | int | `30` | Clamped to `5..240`. |

## 6. UI Fields (`Ui` object)

| Field | Type | Default | Notes |
|---|---|---:|---|
| `Ui.EnableSecretMenu` | bool | `false` | Shows secret diagnostics/settings button. |
| `Ui.AppTitle` | string | `Notifications` | Window title. |

## 7. Legacy/Compatibility Fields

The config model retains these for compatibility with the non-widget branch:

- `ApiKey`, `ApiBaseUrl`, `MaxPosts`, `RequestTimeoutMs`, `RefreshMs`
- `ManualCloseCooldownMs`, `PulseOnNewMessage`, `PulseMinIntervalMs`
- `ForceForegroundOnUrgent`, `UrgentFocusUnreadDelta`, `FocusStealCooldownMs`, `EnableViewTracking`

They are not primary controls for widget rendering.

## 8. Identity Source Priority

For Beamer identity used by the widget:

1. `UserId` (if set)
2. `ViewerUserId` (if set)
3. `Cthisreg` from `C:\TEMP\cheader.dbf`
4. `Environment.UserName`

For display name:

1. `ViewerName` (if set)
2. `Cname` from `C:\TEMP\cheader.dbf`
3. `Environment.MachineName`
