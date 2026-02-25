# User Guide

## 1. What Notifications Does

Notifications is a desktop app that displays Beamer posts in a single feed. It automatically refreshes, highlights unread posts, and lets you acknowledge posts with `OK`.

When you click `OK`:

- The post remains visible.
- The post switches to the read style (gray).
- The `OK` button is removed for that post.
- A view event is sent to Beamer (if tracking is enabled).

## 2. Launching the App

1. Run `Notifications.exe`.
2. Wait for status to show a successful sync message.
3. Confirm posts appear in the feed.

![Main window after launch](screenshots/01-main-window-connected.png)

## 3. Main Screen Overview

The main screen has:

- Header title (customizable)
- `UNREAD POSTS` counter
- Last sync panel
- Post cards (unread and read)

Unread posts are shown in a blue gradient and include an `OK` button.

![Unread post card with OK button](screenshots/02-unread-post-with-ok.png)

## 4. Marking a Post as Read

1. Click `OK` on an unread post.
2. The card turns gray.
3. The `OK` button is removed.
4. `UNREAD POSTS` decreases.

![Read post card after OK](screenshots/03-read-post-gray-no-ok.png)

## 5. No New Posts State

If no posts are available, the feed shows:

`NO NEW POSTS`

![No new posts state](screenshots/04-no-new-posts.png)

## 6. Secret Menu (Theme and Title Customization)

The secret settings menu is only visible when:

- `Ui.EnableSecretMenu=true` in `Notifications.config.json`

When enabled:

1. Click `SETTINGS`.
2. Update titles/colors.
3. Click `Save & Close`.

Important behavior:

- Opening the menu does **not** change theme by itself.
- The UI remains on the current theme until save succeeds.
- `Save & Close` automatically sets `Ui.EnableSecretMenu=false`.

![Settings button visible](screenshots/05-settings-button-visible.png)

![Secret menu open](screenshots/06-secret-menu-open.png)

## 7. How Refresh Works

- Refresh interval is controlled by `RefreshMs`.
- Default in the template is `5000` (5 seconds).
- The app polls unread count and fetches posts when needed.

## 8. Foreground Behavior on New Messages

When a new message is detected, the app restores and brings itself to the front, including from minimized state.

![Window brought to front on new post](screenshots/07-window-brought-to-front.png)

## 9. Message History Retention

Notifications keeps a local archive of posts in:

`MESSAGE_ARCHIVE` (next to the executable)

This lets older posts remain readable even if they expire or are deleted in Beamer.

![Archive files on disk](screenshots/08-message-archive-folder.png)

## 10. What to Do if Posts Do Not Appear

1. Check status panel text for errors.
2. Verify `ApiKey` is set in config.
3. Verify internet access to Beamer endpoints.
4. Restart the app after config changes.

For detailed fault handling:

`/Users/scottwells/Documents/Notifications/docs/TROUBLESHOOTING.md`
