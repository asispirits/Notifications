# Branding and Customization Guide

This guide is for developers who need to adapt Notifications for one or more customer brands.

## 1. Customization Levels

### Level 1: No code changes (recommended)

Use `Notifications.config.json` only:

- `Ui.AppTitle`
- `Ui.HeaderTitle`
- `Ui.Theme*` colors
- optional identity overrides (`ViewerName`, `ViewerUserId`, `ViewerEmail`)

Use this level when brand differences are visual/text only.

### Level 2: Asset and packaging changes

Update icon/manifest/project metadata:

- app icon (`Assets/Spirits.ico` or replacement)
- `ApplicationIcon` in `Notifications.csproj`
- assembly identity in `app.manifest`

Use this level when each brand needs a unique executable identity.

### Level 3: Code-level brand behavior

Change behavior in `MainWindow.xaml.cs` and `index.html` only when branding requires functional differences.

Examples:

- custom acknowledgement behavior
- additional message fields
- custom action buttons

## 2. White-Label Workflow (Per Brand)

1. Copy baseline config and create brand-specific config profile.
2. Set brand titles/colors under `Ui`.
3. Run app with `Ui.EnableSecretMenu=true` for first-time tuning.
4. Adjust visual settings in app and click `Save & Close`.
5. Capture resulting `Ui` values from config.
6. Set `Ui.EnableSecretMenu=false` for release.
7. Publish and package brand build.

## 3. Brand Config Template

Example `Ui` block to clone per brand:

```json
"Ui": {
  "EnableSecretMenu": false,
  "AppTitle": "Brand Name",
  "HeaderTitle": "Brand Name",
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
```

## 4. Important Behavior Notes

- Opening secret menu must not change current theme by itself.
- Theme/title are applied after host confirms save and sends updated `ui_config`.
- `Save & Close` disables secret menu automatically for next launch.

## 5. Multi-Brand Delivery Strategy

For multiple customer brands, keep a single codebase and produce brand packages by swapping config and assets at publish time.

Recommended folder structure:

- `/brand-profiles/<brand>/Notifications.config.json`
- `/brand-profiles/<brand>/Assets/<icon>`
- `/release/<brand>/...`

## 6. Brand Release Procedure

1. Build publish output:

```bash
cd /Users/scottwells/Documents/Notifications
dotnet publish Notifications.csproj -c Release -r win-x64 --self-contained true -nologo
```

2. Copy brand config into publish folder as `Notifications.config.json`.
3. If required, replace icon assets and rebuild.
4. Zip package with brand-specific name:

```bash
cd /Users/scottwells/Documents/Notifications/bin/Release/net8.0-windows/win-x64/publish
zip -r -FS /Users/scottwells/Documents/Notifications/Notifications_<brand>_win-x64_publish.zip .
```

## 7. When to Rename App/Binary for a Brand

If a brand requires distinct executable identity, update:

- `Notifications.csproj`
  - `AssemblyName`
  - `RootNamespace`
- solution/project filenames (optional but recommended)
- `app.manifest` assembly name
- config file name references in `Config.cs`
- docs and build scripts

Only do this if contractually required; otherwise keep binary name stable and brand via config.

## 8. Safe Customization Boundaries

Low-risk changes:

- `Ui` colors/titles
- icon replacement
- text labels in `index.html`

Higher-risk changes (require regression testing):

- message contract changes (`mark_read`, `state`, `ui_config`)
- polling/timing changes in `MainWindow.xaml.cs`
- tracking endpoint behavior
- archive merge/key logic

## 9. Regression Checklist for Brand Changes

1. App launches and shows posts.
2. `UNREAD POSTS` count updates.
3. `OK` marks card read and removes button.
4. Views eventually appear in Beamer analytics.
5. Secret menu hidden in production (`Ui.EnableSecretMenu=false`).
6. Window foreground behavior still works on new post.
7. Archive JSON files still written.
8. No hardcoded old brand strings remain.

## 10. Repository Rename and Coordination

If repository name changes to `Notifications`, update remote URL:

```bash
cd /Users/scottwells/Documents/Notifications
git remote set-url origin https://github.com/asispirits/Notifications.git
git remote -v
```

Also update any CI/CD scripts that reference the old path or old zip filename.
