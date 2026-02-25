# Operations Runbook

## 1. Objective

This runbook covers installation, configuration, validation, and support procedures for production use of Notifications.

## 2. Deployment Package Contents

Expected package:

- `Notifications.exe`
- runtime dependencies (from publish folder)
- `Notifications.config.json`

Optional:

- `MESSAGE_ARCHIVE` (if migrating existing local history)

## 3. Installation Procedure (Windows Endpoint)

1. Copy publish folder to target machine.
2. Ensure write permission for app directory.
3. Edit `Notifications.config.json` with environment-specific values.
4. Launch `Notifications.exe`.
5. Validate status and post feed.

![Installed app in running state](screenshots/09-installed-running-state.png)

## 4. Required Configuration

Must set:

- `ApiKey`

Strongly recommended:

- `ProductId` (confirm correct Beamer product)
- `RefreshMs` (based on environment expectations)
- `ViewerName` / `ViewerUserId` overrides if DBF source not available

## 5. Identity Verification Procedure

If NAME/USER ID in Beamer appears incorrect:

1. Check if `ViewerName`/`ViewerUserId` overrides are present in config.
2. If blank, verify `C:\TEMP\cheader.dbf` exists and includes fields:
   - `Cname`
   - `Cthisreg`
3. Restart the app.

## 6. Archive Verification Procedure

1. Confirm `MESSAGE_ARCHIVE` folder exists beside executable.
2. Confirm JSON files are being written as posts are processed.
3. Open app and verify archived/live merge behavior.

![Archive folder with JSON files](screenshots/10-archive-folder-json-files.png)

## 7. Change-Control Procedure for Theme/Branding

1. Temporarily set `Ui.EnableSecretMenu=true`.
2. Restart app and open `SETTINGS`.
3. Apply changes and click `Save & Close`.
4. Confirm `Ui.EnableSecretMenu` flips to `false`.
5. Record final color/title values in a controlled copy of config.

## 8. Routine Health Checks

Daily or weekly checks:

- App opens without API error
- Last sync updates regularly
- New posts appear without restart
- `OK` updates read state correctly
- Beamer view activity eventually reflects acknowledgements

Note: Beamer analytics reporting may appear delayed.

## 9. Incident Response Matrix

### Symptom: Missing posts

- Validate `ApiKey`, connectivity, and product ID.
- Confirm status panel message.
- Check `TROUBLESHOOTING.md` flow.

### Symptom: View events not visible immediately

- Confirm `EnableViewTracking=true`.
- Confirm user clicks `OK`.
- Wait for Beamer analytics refresh window.

### Symptom: Secret menu visible unexpectedly

- Check `Ui.EnableSecretMenu` in config.
- Confirm updated build includes hidden-state fix.

## 10. Upgrade Procedure

1. Stop running app.
2. Backup existing:
   - `Notifications.config.json`
   - `MESSAGE_ARCHIVE` folder
3. Replace files with new publish package.
4. Restore/merge config values.
5. Launch and validate.

## 11. Backup and Recovery

Backup target:

- `Notifications.config.json`
- `MESSAGE_ARCHIVE/*.json`

Recovery:

- Restore config and archive files to same paths.
- Restart app.
