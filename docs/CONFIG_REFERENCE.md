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

## 2. Full Config Example

```json
{
  "ProductId": "vEjlRlWp82033",
  "UserId": "",
  "ViewerUserId": "",
  "ViewerName": "",
  "ViewerEmail": "",
  "ApiKey": "",
  "ApiBaseUrl": "https://api.getbeamer.com/v0",
  "MaxPosts": 25,
  "RequestTimeoutMs": 10000,
  "RefreshMs": 5000,
  "WidgetWidth": 520,
  "AutoOpenOnLaunch": true,
  "ManualCloseCooldownMs": 6000,
  "PulseOnNewMessage": true,
  "PulseMinIntervalMs": 15000,
  "ForceForegroundOnUrgent": false,
  "UrgentFocusUnreadDelta": 3,
  "FocusStealCooldownMs": 45000,
  "EnableViewTracking": true,
  "Ui": {
    "EnableSecretMenu": false,
    "AppTitle": "Notifications",
    "HeaderTitle": "Notifications",
    "ThemePageBackground": "#ffffff",
    "ThemeHeaderStart": "#ffffff",
    "ThemeHeaderEnd": "#f5faff",
    "ThemeTextMain": "#1a4e86",
    "ThemeTextMuted": "#4b6f95",
    "ThemeAccent": "#0000ff",
    "ThemeAccentSoft": "#80ffff",
    "ThemeUnreadStart": "#6bbbf6",
    "ThemeUnreadEnd": "#519fdd",
    "ThemeReadStart": "#758499",
    "ThemeReadEnd": "#657486"
  }
}
```

## 3. Core Fields

| Field | Type | Default | Notes |
|---|---|---:|---|
| `ProductId` | string | `vEjlRlWp82033` | Required for API and tracking. |
| `UserId` | string | `""` | If blank, app generates `local-<guid>` internally. |
| `ViewerUserId` | string | `""` | Override identity sent to Beamer. |
| `ViewerName` | string | `""` | Override display name sent to Beamer. |
| `ViewerEmail` | string | `""` | Optional user email for tracking payloads. |
| `ApiKey` | string | `""` | Required for successful post fetches. |
| `ApiBaseUrl` | string | `https://api.getbeamer.com/v0` | Trimmed and trailing `/` removed. |
| `MaxPosts` | int | `25` | Clamped to `1..100`. |
| `RequestTimeoutMs` | long | `10000` | Clamped to `2000..60000`. |
| `RefreshMs` | long | `60000` internal default; template currently `5000` | Clamped to minimum `1000`. |
| `WidgetWidth` | int | `520` | If `<320`, reset to `520`. |
| `AutoOpenOnLaunch` | bool | `true` | Present for compatibility. |
| `ManualCloseCooldownMs` | long | `6000` | Clamped to `0..120000`. |
| `PulseOnNewMessage` | bool | `true` | Present for compatibility. |
| `PulseMinIntervalMs` | long | `15000` | Clamped to `0..600000`. |
| `ForceForegroundOnUrgent` | bool | `false` | Present; new-message foreground is currently enforced directly. |
| `UrgentFocusUnreadDelta` | int | `3` | Clamped to `1..50`. |
| `FocusStealCooldownMs` | long | `45000` | Clamped to `0..600000`. |
| `EnableViewTracking` | bool | `true` | Controls posting read/view tracking to Beamer. |

## 4. UI Fields (`Ui` object)

| Field | Type | Default | Validation |
|---|---|---:|---|
| `Ui.EnableSecretMenu` | bool | `false` | When `true`, shows `SETTINGS` button. |
| `Ui.AppTitle` | string | `Notifications` | Trimmed; max length 80. |
| `Ui.HeaderTitle` | string | `Notifications` | Trimmed; max length 80. |
| `Ui.ThemePageBackground` | string | `#ffffff` | Must match `#RRGGBB`. |
| `Ui.ThemeHeaderStart` | string | `#ffffff` | Must match `#RRGGBB`. |
| `Ui.ThemeHeaderEnd` | string | `#f5faff` | Must match `#RRGGBB`. |
| `Ui.ThemeTextMain` | string | `#1a4e86` | Must match `#RRGGBB`. |
| `Ui.ThemeTextMuted` | string | `#4b6f95` | Must match `#RRGGBB`. |
| `Ui.ThemeAccent` | string | `#0000ff` | Must match `#RRGGBB`. |
| `Ui.ThemeAccentSoft` | string | `#80ffff` | Must match `#RRGGBB`. |
| `Ui.ThemeUnreadStart` | string | `#6bbbf6` | Must match `#RRGGBB`. |
| `Ui.ThemeUnreadEnd` | string | `#519fdd` | Must match `#RRGGBB`. |
| `Ui.ThemeReadStart` | string | `#758499` | Must match `#RRGGBB`. |
| `Ui.ThemeReadEnd` | string | `#657486` | Must match `#RRGGBB`. |

## 5. Legacy Compatibility

The loader also reads legacy top-level UI keys when present:

- `EnableSecretMenu`
- `UiAppTitle`
- `UiHeaderTitle`
- `Theme*`

These are mapped into `Ui.*` at load time.

## 6. Identity Source Priority

For viewer identity sent to Beamer:

1. `ViewerName` / `ViewerUserId` from config (if non-empty)
2. `Cname` / `Cthisreg` from `C:\TEMP\cheader.dbf`
3. `Environment.MachineName` / `Environment.UserName` fallback

## 7. Secret Menu Save Behavior

When the user clicks `Save & Close` in the secret menu:

- UI settings are saved to config.
- `Ui.EnableSecretMenu` is forced to `false`.
- Menu closes.

If save fails, `Ui.EnableSecretMenu` is not flipped.
