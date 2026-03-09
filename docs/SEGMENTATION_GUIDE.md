# Segmentation Setup Guide (Single Document)

Use this guide to configure endpoint segmentation and send targeted Beamer messages.

## 1. What This Does

Segmentation lets you send a message to only specific endpoints (for example one site, one region, or one role).

Your endpoint receives a post only when the post audience in Beamer matches one or more endpoint segment tags.

On first launch, `Notifications.exe` now auto-initializes segmentation once and writes values into `Notifications.config.json`.

## 2. Required Files

- App config: `Notifications.config.json`
- PowerShell generator: `scripts/New-SegmentFilters.ps1`
- PowerShell apply script: `scripts/Set-NotificationsEndpointConfig.ps1`
- Batch wrappers:
  - `scripts/Generate-SegmentFilters.bat`
  - `scripts/Apply-EndpointConfig.bat`
  - `scripts/Configure-EndpointSegmentation.bat`
- Optional inventory template: `docs/segment_inventory_template.csv`

## 3. Segment Naming Standard

Use lowercase tags with `key:value` format.

Recommended keys:

- `tenant:<name>`
- `brand:<name>`
- `region:<name>`
- `site:<id>`
- `role:<name>`
- `env:<name>`

Example values:

- `tenant:asi`
- `brand:spirits`
- `region:east`
- `site:store-104`
- `role:cashier`
- `env:prod`

## 4. Configure One Endpoint (Fast Path)

Run these on Windows.

### Option A (Recommended): One-step `.bat`

```bat
cd <repo-root>\scripts
Configure-EndpointSegmentation.bat "C:\Program Files\Notifications\Notifications.config.json" "store-104-pos-01" "asi" "spirits" "east" "store-104" "cashier" "prod"
```

Usage:

```bat
Configure-EndpointSegmentation.bat <ConfigPath> <UserId> <Tenant> <Brand> <Region> <Site> <Role> [Environment] [ExtraSegments]
```

### Option B: Two-step `.bat` wrappers

Step B1: Generate segment tags

```bat
cd <repo-root>\scripts
Generate-SegmentFilters.bat -UserId "store-104-pos-01" -Tenant "asi" -Brand "spirits" -Region "east" -Site "store-104" -Role "cashier" -Environment "prod"
```

Copy the `SegmentFilters` value from output.

Step B2: Apply values to config

```bat
cd <repo-root>\scripts
Apply-EndpointConfig.bat -ConfigPath "C:\Program Files\Notifications\Notifications.config.json" -UserId "store-104-pos-01" -SegmentFilters "tenant:asi;brand:spirits;region:east;site:store-104;role:cashier;env:prod" -IncludeViewerUserIdSegment:$false -SegmentMultiUser:$true
```

### Option C: PowerShell directly

```powershell
cd <repo-root>\scripts
.\New-SegmentFilters.ps1 -UserId "store-104-pos-01" -Tenant "asi" -Brand "spirits" -Region "east" -Site "store-104" -Role "cashier" -Environment "prod"
.\Set-NotificationsEndpointConfig.ps1 -ConfigPath "C:\Program Files\Notifications\Notifications.config.json" -UserId "store-104-pos-01" -SegmentFilters "tenant:asi;brand:spirits;region:east;site:store-104;role:cashier;env:prod" -IncludeViewerUserIdSegment:$false -SegmentMultiUser:$true
```

### Final step

Restart `Notifications.exe` after config updates.

## 5. Send a Segmented Message in Beamer

1. Open Beamer and create a new post.
2. In audience/targeting, add segment filters that match endpoint tags.
3. Publish.

Examples:

- Only east region: `region:east`
- Only one site: `site:store-104`
- Managers at one site: `site:store-104` and `role:manager`
- All production devices: `env:prod`

## 6. Validate Delivery

Use this exact test flow:

1. Pick 2 endpoints:
   - one that should match
   - one that should not match
2. Publish a targeted test post.
3. Confirm:
   - matching endpoint shows post
   - non-matching endpoint does not show post
4. Wait up to ~15 minutes for Beamer view analytics to appear.

## 7. Common Settings (What To Use)

Set these in `Notifications.config.json`:

- `UserId`: stable unique endpoint id
- `SegmentFilters`: semicolon-delimited tags
- `IncludeViewerUserIdSegment`: usually `false`
- `SegmentMultiUser`: usually `true`
- `SegmentForceFilter`: leave empty unless explicitly needed

Example block:

```json
{
  "UserId": "store-104-pos-01",
  "SegmentFilters": "tenant:asi;brand:spirits;region:east;site:store-104;role:cashier;env:prod",
  "IncludeViewerUserIdSegment": false,
  "SegmentMultiUser": true,
  "SegmentForceFilter": ""
}
```

## 8. Roll Out to Many Endpoints

1. Fill `docs/segment_inventory_template.csv` with each endpoint.
2. Run `Configure-EndpointSegmentation.bat` per endpoint.
3. Restart app on each endpoint.
4. Run one regional and one site-level test campaign.

## 9. Troubleshooting

If endpoint does not receive expected posts:

1. Confirm `UserId` and `SegmentFilters` in endpoint `Notifications.config.json`.
2. Confirm Beamer audience filters exactly match tag text.
3. Confirm app was restarted after config edit.
4. Confirm endpoint is using the intended `ProductId`.

If views are delayed:

- This is expected in Beamer analytics. Views may appear after a delay.

If first-run setup did not write segmentation values:

1. Confirm `SegmentationInitialized` is `false` before first launch.
2. Confirm scripts exist under `<publish-folder>\scripts`.
3. Relaunch app and check `Notifications.config.json` for `UserId` and `SegmentFilters`.
