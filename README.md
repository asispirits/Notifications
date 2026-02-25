# Notifications

Notifications is a Windows desktop app (WPF + WebView2) that displays Beamer posts in a local UI, tracks acknowledged views, and preserves message history in a local archive.

## Quick Start

### Build

```bash
cd /Users/scottwells/Documents/Notifications
dotnet build Notifications.csproj -c Release -nologo
```

### Publish

```bash
cd /Users/scottwells/Documents/Notifications
dotnet publish Notifications.csproj -c Release -r win-x64 --self-contained true -nologo
```

Published executable:

`/Users/scottwells/Documents/Notifications/bin/Release/net8.0-windows/win-x64/publish/Notifications.exe`

## Config File

Primary config file:

`/Users/scottwells/Documents/Notifications/Notifications.config.json`

At minimum, set:

- `ApiKey`

## Documentation Suite

Complete documentation is under:

`/Users/scottwells/Documents/Notifications/docs/README.md`

Direct links:

- User guide: `/Users/scottwells/Documents/Notifications/docs/USER_GUIDE.md`
- Config reference: `/Users/scottwells/Documents/Notifications/docs/CONFIG_REFERENCE.md`
- Technical architecture: `/Users/scottwells/Documents/Notifications/docs/TECHNICAL_ARCHITECTURE.md`
- API/message contracts: `/Users/scottwells/Documents/Notifications/docs/API_AND_MESSAGE_CONTRACTS.md`
- Operations runbook: `/Users/scottwells/Documents/Notifications/docs/OPERATIONS_RUNBOOK.md`
- Troubleshooting: `/Users/scottwells/Documents/Notifications/docs/TROUBLESHOOTING.md`
- Developer guide: `/Users/scottwells/Documents/Notifications/docs/DEVELOPER_GUIDE.md`
- Branding customization guide: `/Users/scottwells/Documents/Notifications/docs/BRANDING_CUSTOMIZATION_GUIDE.md`
- Screenshot checklist: `/Users/scottwells/Documents/Notifications/docs/SCREENSHOT_CHECKLIST.md`
