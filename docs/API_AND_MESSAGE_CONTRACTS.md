# API and Message Contracts

## 1. External HTTP Endpoints

### Beamer content

- `GET {ApiBaseUrl}/posts`
- `GET {ApiBaseUrl}/unread/count`

### Tracking

- `POST https://app.getbeamer.com/trackViews?...` (numeric `descriptionId` path)
- `POST https://backend.getbeamer.com/track?...` (query tracking path)

### Description ID fallback cache source

- `GET https://app.getbeamer.com/loadMoreNews?app_id={ProductId}`

## 2. Auth Modes Attempted by Host

In fallback order:

1. `Authorization: Bearer <ApiKey>`
2. `X-Beamer-API-Key` and `Beamer-API-Key`
3. `X-API-Key` and `API-Key`
4. `api_key` query parameter

## 3. Host -> UI Messages

### `state`

```json
{
  "type": "state",
  "payload": {
    "status": "ok|loading|error|config_error",
    "message": "string",
    "refreshMs": 5000,
    "configPath": "string",
    "lastSyncUtc": "2026-02-25T00:00:00.0000000Z",
    "posts": [],
    "newPostIds": []
  }
}
```

### `archive_state`

```json
{
  "type": "archive_state",
  "payload": {
    "messages": []
  }
}
```

### `ui_config`

```json
{
  "type": "ui_config",
  "payload": {
    "enable_secret_menu": false,
    "ui_app_title": "Notifications",
    "ui_header_title": "Notifications",
    "theme_page_background": "#ffffff",
    "theme_header_start": "#ffffff",
    "theme_header_end": "#f5faff",
    "theme_text_main": "#1a4e86",
    "theme_text_muted": "#4b6f95",
    "theme_accent": "#0000ff",
    "theme_accent_soft": "#80ffff",
    "theme_unread_start": "#6bbbf6",
    "theme_unread_end": "#519fdd",
    "theme_read_start": "#758499",
    "theme_read_end": "#657486"
  }
}
```

### `ui_settings_saved`

```json
{
  "type": "ui_settings_saved",
  "payload": {
    "success": true,
    "message": "UI settings saved. Secret menu disabled."
  }
}
```

## 4. UI -> Host Messages

### `ui_ready`

```json
{
  "type": "ui_ready",
  "payload": {}
}
```

### `open_external`

```json
{
  "type": "open_external",
  "payload": {
    "url": "https://..."
  }
}
```

### `mark_read`

```json
{
  "type": "mark_read",
  "payload": {
    "id": "string",
    "code": "string",
    "beaconTracked": false,
    "trackIds": ["string"],
    "postKey": "string",
    "title": "string",
    "content": "string",
    "category": "string",
    "dateUtc": "string",
    "postUrl": "string",
    "linkUrl": "string",
    "linkText": "string"
  }
}
```

### `save_ui_settings`

```json
{
  "type": "save_ui_settings",
  "payload": {
    "appTitle": "Notifications",
    "headerTitle": "Notifications",
    "themePageBackground": "#ffffff",
    "themeHeaderStart": "#ffffff",
    "themeHeaderEnd": "#f5faff",
    "themeTextMain": "#1a4e86",
    "themeTextMuted": "#4b6f95",
    "themeAccent": "#0000ff",
    "themeAccentSoft": "#80ffff",
    "themeUnreadStart": "#6bbbf6",
    "themeUnreadEnd": "#519fdd",
    "themeReadStart": "#758499",
    "themeReadEnd": "#657486",
    "disableSecretMenuAfterSave": true
  }
}
```
