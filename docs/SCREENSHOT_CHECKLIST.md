# Screenshot Checklist

This checklist defines exactly what screenshots to capture and where to place them.

## 1. Destination Folder

Save all screenshots to:

`docs/screenshots`

Use the exact filenames below.

## 2. Capture Rules

- Capture on a test environment with representative data.
- Mask/redact sensitive values (`ApiKey`, personal IDs, internal URLs) before committing.
- Prefer PNG format.
- Keep window width around standard desktop size so text is readable.
- Include full app window unless a specific crop is requested.

## 3. Required Screenshots

| File name | What to capture | Used in |
|---|---|---|
| `01-main-window-connected.png` | App launched, connected, showing at least one post | `USER_GUIDE.md` |
| `02-unread-post-with-ok.png` | Unread card in blue style with visible `OK` button | `USER_GUIDE.md` |
| `03-read-post-gray-no-ok.png` | Same/similar card after `OK` (gray, no button) | `USER_GUIDE.md` |
| `04-no-new-posts.png` | Feed showing `NO NEW POSTS` | `USER_GUIDE.md` |
| `05-settings-button-visible.png` | Header with `SETTINGS` button visible (`Ui.EnableSecretMenu=true`) | `USER_GUIDE.md` |
| `06-secret-menu-open.png` | Secret settings modal open with color/title controls | `USER_GUIDE.md` |
| `07-window-brought-to-front.png` | Example of app in front after receiving new post | `USER_GUIDE.md` |
| `08-message-archive-folder.png` | File Explorer/Finder view of `MESSAGE_ARCHIVE` folder | `USER_GUIDE.md` |
| `09-installed-running-state.png` | Running app from deployed publish folder | `OPERATIONS_RUNBOOK.md` |
| `10-archive-folder-json-files.png` | Archive folder showing JSON files with timestamps | `OPERATIONS_RUNBOOK.md` |
| `11-error-missing-apikey.png` | Status panel with missing API key/config error | `TROUBLESHOOTING.md` |
| `12-error-auth-or-api.png` | Status panel with API/auth error response | `TROUBLESHOOTING.md` |
| `13-blank-content-state.png` | Blank/white content troubleshooting example | `TROUBLESHOOTING.md` |

## 4. Optional (Nice-to-Have)

| File name | What to capture | Candidate use |
|---|---|---|
| `14-config-file-open.png` | `Notifications.config.json` open in editor | User/admin setup docs |
| `15-beamer-view-confirmation.png` | Beamer side showing view acknowledgement for a post | Runbook validation section |

## 5. Quick Capture Workflow

1. Set `Ui.EnableSecretMenu=true` in config for settings screenshots.
2. Launch app and capture base UI states (`01` to `07`).
3. Capture filesystem/archive states (`08`, `10`).
4. Temporarily trigger known error states for troubleshooting captures (`11` to `13`).
5. Set `Ui.EnableSecretMenu=false` and retest normal mode.

## 6. Verification

After images are captured, run:

```bash
cd <repo-root>
ls -1 docs/screenshots
```

Confirm all required filenames exist exactly as listed.
