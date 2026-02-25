# Troubleshooting Guide

## 1. Missing API Key Error

### Symptom

Status shows config error similar to:

`Missing ApiKey. Add your private Beamer API key in the config file and restart.`

### Actions

1. Open `Notifications.config.json`.
2. Set `ApiKey` to a valid Beamer private API key.
3. Save and restart app.

![Missing API key error state](screenshots/11-error-missing-apikey.png)

## 2. Authentication Failed / No Posts Returned

### Symptom

- Status error from Beamer API.
- No posts shown.

### Actions

1. Verify `ApiKey` is valid and not expired.
2. Verify `ProductId` is the intended Beamer app/product.
3. Confirm `ApiBaseUrl` remains `https://api.getbeamer.com/v0`.
4. Confirm machine can reach Beamer endpoints.

![Authentication or API error state](screenshots/12-error-auth-or-api.png)

## 3. New Posts Not Showing Quickly

### Symptom

- New posts appear late.

### Actions

1. Verify `RefreshMs` value.
2. Confirm app stays running (not suspended).
3. Confirm API call success in status panel.
4. Restart app and confirm immediate fetch works.

## 4. Views Not Appearing in Beamer Immediately

### Symptom

- User clicks `OK` but Beamer view count does not update right away.

### Actions

1. Verify `EnableViewTracking=true`.
2. Confirm `OK` was clicked on post card.
3. Wait for Beamer analytics ingestion delay.
4. Recheck Beamer UI after the delay window.

## 5. Secret Menu Visibility Issue

### Symptom

`SETTINGS` button appears when it should not.

### Actions

1. Open config and check `Ui.EnableSecretMenu`.
2. Set it explicitly to `false`.
3. Restart app.
4. Verify build includes hidden-state fix.

## 6. Secret Menu Changes Theme Before Save

### Symptom

Theme changes as soon as settings dialog opens.

### Expected behavior

Theme should remain unchanged until save completes.

### Actions

1. Use current build (contains no-preview save behavior).
2. If issue persists, report build version and reproduction steps.

## 7. Blank/White Content Area

### Symptom

Window opens, but feed area is blank/white.

### Actions

1. Close and relaunch app.
2. Verify internet access and API key.
3. Ensure local app folder is writable.
4. Ensure `index.html` resource exists in current build.

![Blank/white content state example](screenshots/13-blank-content-state.png)

## 8. NAME / USER ID in Beamer Incorrect

### Symptom

Beamer shows wrong identity values.

### Actions

1. Check config overrides:
   - `ViewerName`
   - `ViewerUserId`
2. If blank, verify `C:\TEMP\cheader.dbf` and fields `Cname`/`Cthisreg`.
3. Restart app after updates.

## 9. Archive Files Not Being Created

### Symptom

`MESSAGE_ARCHIVE` folder missing or empty.

### Actions

1. Confirm app can write to executable directory.
2. Verify posts are actually being fetched.
3. Trigger a fresh sync and check folder again.

## 10. Diagnostic Bundle for Support

When escalating an issue, provide:

- screenshot of app state
- active `Notifications.config.json` (redact API key)
- sample files from `MESSAGE_ARCHIVE`
- exact time and timezone of test
- whether message was acknowledged via `OK`
