# Developer Guide

## 1. Prerequisites

- .NET SDK 8+
- NuGet access
- macOS/Linux/Windows for build orchestration
- Windows runtime for app execution testing (WPF)

## 2. Solution Layout

- `Notifications.sln`
- `Notifications.csproj`
- `MainWindow.xaml`
- `MainWindow.xaml.cs`
- `index.html`
- `Config.cs`
- `Notifications.config.json`

## 3. Build and Publish

### Build

```bash
cd <repo-root>
dotnet build Notifications.csproj -c Release -nologo
```

### Publish

```bash
cd <repo-root>
dotnet publish Notifications.csproj -c Release -r win-x64 --self-contained true -nologo
```

Publish output folder:

`bin/Release/net8.0-windows/win-x64/publish`

Primary binary:

`bin/Release/net8.0-windows/win-x64/publish/Notifications.exe`

## 4. Packaging a Distribution ZIP

```bash
cd <repo-root>/bin/Release/net8.0-windows/win-x64/publish
cp -f <repo-root>/Notifications.config.json ./
zip -r -FS <repo-root>/Notifications_win-x64_publish.zip .
```

## 5. Host/UI Responsibilities

### `MainWindow.xaml.cs` (host)

- Config and identity load
- Polling and API requests
- Tracking calls
- Archive persistence
- WebView message bridge
- Window focus behavior

### `index.html` (frontend)

- Render feed/status
- Maintain read-state in browser local storage
- Send `mark_read` and settings save messages
- Apply theme and title updates from host config

## 6. Read-State Storage

The frontend stores acknowledged post keys in browser localStorage key:

`bv_read_post_ids_v3`

This is local to the WebView profile on that machine/profile.

## 7. Working on Config Schema

When adding config fields:

1. Add property in `Config.cs`.
2. Update normalization logic and bounds checks.
3. Update template: `Notifications.config.json`.
4. If field affects frontend, include in `InjectConfigAndBootAsync` payload.
5. Update docs in:
   - `CONFIG_REFERENCE.md`
   - `USER_GUIDE.md` (if user visible)

## 8. Brand Customization (Recommended Path)

For white-label or customer-specific branding, use this playbook:

`./BRANDING_CUSTOMIZATION_GUIDE.md`

Default recommendation:

- Keep one codebase.
- Brand through `Notifications.config.json` `Ui` values first.
- Only perform code-level branding when required.

## 9. Repository Rename Guidance (GitHub)

If GitHub repository is renamed from `Beamer-Viewer` to `Notifications`, update local remote URL:

```bash
cd <repo-root>
git remote -v
git remote set-url origin https://github.com/asispirits/Notifications.git
git remote -v
```

If the old URL still redirects, this is optional but recommended for clarity.

## 10. Code Areas Most Likely to Need Coordinated Changes

- Message payload contracts (`index.html` + `MainWindow.xaml.cs`)
- Config fields (`Config.cs` + template + docs)
- Identity mapping (`TryLoadCHeaderIdentity` + tracking payload)
- Polling/focus behavior (`PollOnceAsync`, `BringToFront`)

## 11. Suggested Validation After Changes

1. Launch app with valid API key.
2. Confirm posts render and `UNREAD POSTS` updates.
3. Click `OK`; confirm card turns gray and button disappears.
4. Verify no runtime errors when secret menu disabled.
5. Enable secret menu, change theme/title, save.
6. Confirm settings persist and menu is disabled after save.
7. Restart app and confirm archive is still visible.
8. Confirm brand-specific title/colors/icons are applied where expected.
