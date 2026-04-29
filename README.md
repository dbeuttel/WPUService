# Workstation Presence Utility (WPUService)

A Windows tray app that **forwards missed Microsoft Teams notifications to your phone or inbox when you step away from your desk**.

When your computer is idle and a Teams notification arrives, WPUService waits a configurable grace period — and if you haven't moved the mouse or typed by the time it expires, it sends you an alert via Outlook, SMTP, or Pushover. Useful for catching urgent messages while you're at lunch, in another building, or in a meeting room without your laptop.

> This is an "I'm away, ping me on my phone" utility. **It is not a presence faker.** It does not interact with Teams' presence state, does not move the mouse, and does not keep your computer awake.

---

## Features

- Detects new Microsoft Teams notifications via the Windows `UserNotificationListener` API
- Configurable **idle threshold** (1 minute → 15 minutes) and **alert delay** (Immediate → 15 minutes)
- Three delivery transports:
  - **Outlook** — uses your installed desktop Outlook profile (no SMTP credentials needed)
  - **SMTP** — Gmail (with app password), Office 365, or any SMTP server
  - **Pushover** — silent, discreet mobile push, no carrier hops
- Optional **SMS** via your carrier's email-to-SMS gateway (T-Mobile, Verizon, AT&T, custom)
- **Pause-on-call** detection so back-to-back notifications don't fire repeated alerts
- DPAPI-encrypted credentials, all data stays local to your user profile
- Single-file, self-contained .NET 9 binary (~55 MB, no separate runtime install)

## Installation

1. Download `WPUService.exe` from the [latest release](https://github.com/dbeuttel/WPUService/releases/latest).
2. Run it. On first launch it will ask whether to install to your user profile (recommended) or run once from the current location.
3. Once installed, it auto-launches at login and lives in your system tray.

No admin rights required. Installs to `%LOCALAPPDATA%\WPUService\`. Configuration goes to `%APPDATA%\WPUService\config.json`.

## Usage

- **Left-click the tray icon** — toggles WPUService on/off.
- **Right-click the tray icon** — opens a small menu with **Open Settings** and **Quit**.
- **Open Settings** — opens the full settings window with everything organized into:
  - **General** — master enable, start with Windows
  - **Detection** — pause on Teams notification, filter (any vs. calls only), idle threshold, alert delay (including **Immediate**)
  - **Delivery method** — Outlook / SMTP / Pushover
  - **Recipient / SMTP / Pushover** — credentials and addresses (encrypted at rest)
  - **Diagnostics** — view notification log, show status, request notification access, simulate a Teams notification (with a live status icon you can watch turn gray on pause), send test alert
  - **Danger zone** — uninstall

## How it works

1. Subscribes to the Windows notification listener for app notifications.
2. When a Microsoft Teams notification arrives, marks you as "paused" and starts a grace timer.
3. If you move the mouse or type a key before the timer expires, the pause clears and no alert is sent.
4. If the timer expires and you haven't reacted, the alert fires via your configured transport, with the notification title and body.
5. Real input is detected via low-level keyboard / mouse hooks so the timer can't be fooled by background activity.

## Building from source

Requirements: **.NET 9 SDK**, **Windows 10/11**.

```bash
git clone https://github.com/dbeuttel/WPUService.git
cd WPUService
dotnet publish -c Release
```

Output: `bin/Release/net9.0-windows10.0.19041.0/win-x64/publish/WPUService.exe`.

The csproj is configured for `PublishSingleFile=true`, `SelfContained=true`, `RuntimeIdentifier=win-x64` — the resulting exe runs on any Windows 10/11 x64 machine without needing the .NET runtime preinstalled.

## Permissions and privacy

- **Notification access** — Windows must grant the app access to the notification history. WPUService will prompt for this on first launch; you can re-request it from **Settings → Diagnostics → Request notification access**.
- **Notification content** — when an alert fires, the title and body of the Teams notification are included in the email/SMS/Pushover message. Only your configured recipient sees this.
- **No telemetry** — the app does not phone home, does not log to any external service, does not collect usage data.
- **No cloud** — all configuration, credentials, and the notification history are stored locally under your user profile.

## Roadmap

- Microsoft Store distribution — see [STORE_PUBLISH.md](./STORE_PUBLISH.md) for the scoped plan.

---

*Not affiliated with, endorsed by, or sponsored by Microsoft Corporation. Microsoft® and Microsoft Teams® are trademarks of the Microsoft group of companies.*
