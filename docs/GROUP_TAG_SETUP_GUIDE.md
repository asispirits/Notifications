# Group Tags Guide (Easy Version)

This guide helps you send one message to a chosen group of computers.

## What is a group tag?

A group tag is a label, for example:

`group:district-7`

If a computer has that label in its settings, it gets that message.
If it does not have that label, it does not get that message.

## Before you start

You need:

- Beamer access
- Access to each computer's config file
- The Notifications app closed while editing settings

Config file location:

`C:\Program Files\Notifications\Notifications.config.json`

## Quick setup (recommended)

Do this on each computer that should get the message.

### Step 1: Choose your group name

Example group names:

- `group:district-7`
- `group:managers-east`
- `group:pilot-sites`

Use lowercase and no spaces.

### Step 2: Add the group tag to each target computer

Open this file:

`C:\Program Files\Notifications\Notifications.config.json`

Find `SegmentFilters` and add your group tag.

Example:

```json
{
  "UserId": "store-104-pos-01",
  "SegmentFilters": "tenant:asi;site:store-104;group:district-7;env:prod"
}
```

Important:

- `UserId` must stay unique on each computer.
- The same `group:...` tag must be on all computers in that group.

### Step 3: Save and restart app

- Save the file.
- Open `Notifications.exe` again.

## Send a message to the group in Beamer

1. Create a new post in Beamer.
2. In Audience/Targeting, add this filter:
   - `group:district-7` (or your group tag)
3. Publish.

Only computers with that group tag will receive it.

## Fast BAT command option

If you prefer command line, run this on each target computer:

```bat
cd "C:\Program Files\Notifications\scripts"
Configure-EndpointSegmentation.bat "C:\Program Files\Notifications\Notifications.config.json" "store-104-pos-01" "asi" "spirits" "east" "store-104" "cashier" "prod" "group:district-7"
```

Then restart `Notifications.exe`.

## Easy test (2 computers)

Use 2 computers:

- Computer A has `group:district-7`
- Computer B does not

Send a Beamer post to `group:district-7`.

Expected result:

- Computer A gets the message
- Computer B does not

## Troubleshooting

If a computer did not get the message:

1. Check spelling: group tag must match exactly.
2. Make sure app was restarted after config change.
3. Make sure `UserId` is not shared with another computer.
4. Confirm Beamer post audience uses the same group tag.

If Beamer views appear later, that is normal. Analytics can be delayed.
