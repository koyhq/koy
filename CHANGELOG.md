# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project follows [Semantic Versioning](https://semver.org/).

Entries up to 0.2.2 are English summaries. The full history of those releases is in the Turkish changelog, [`CHANGELOG.tr.md`](CHANGELOG.tr.md).

---

## [0.2.2](https://github.com/koyhq/koy/compare/v0.2.1...v0.2.2) (2026-11-08)

Highlights:
- **Easier-to-read release notes:** "What's New" and the update window no longer show links, commit hashes and technical labels, and long lines are no longer cut off.

## [0.2.1](https://github.com/koyhq/koy/compare/v0.2.0...v0.2.1) (2026-11-08)

Highlights:
- **Jump list:** Right-clicking an app on the dock shows recent files, frequent items and the app's own tasks, as in Windows.
- **Always floating:** The floating capsule stays in the air even when a window comes close or is maximized.
- **Privacy dots:** A green dot in the notch while the camera is in use and an orange dot for the microphone; hover to see which app is using it.
- **New update flow:** Updates no longer download on their own. Koy asks first, shows what changed and displays download progress.
- **More stable dock:** With multiple displays, "Dock down" no longer lifts when focus moves to the other display, and Koy no longer changes the taskbar auto-hide setting when the dock is off.

## [0.2.0](https://github.com/koyhq/koy/compare/v0.1.1...v0.2.0) (2026-11-07)

Highlights:
- **Dock folders:** Drag a pinned app onto another to make a folder, shown on the dock with a 2×2 preview.
- **Windows-style full-width dock:** 48 px, attached to the screen edge, with "Always show" or "Auto hide".
- **Compact tray, press feel and accent color:** The tray is a narrow arrow next to the Koy button, icons dip when pressed, and your accent color is used on the dock and notch.
- **Notifications in the notch:** Charger plugged in or removed, low and full battery, and Bluetooth headphones connecting with their name and battery.
- **More stable dock:** Maximized windows no longer resize on their own, and changing a setting no longer restarts the dock.
- **Repository moved:** Koy now lives at [github.com/koyhq/koy](https://github.com/koyhq/koy). The old address redirects.

## [0.1.1](https://github.com/koyhq/koy/compare/v0.1.0...v0.1.1) (2026-11-06)

Highlights:
- New notch design with 7 tabs (Home, Focus, Calendar, Shelf, Clipboard, System, Control).
- Wi-Fi and Bluetooth lists, as in Windows.
- Bars that move with the music and an expandable music card.
- A watchdog process that brings the taskbar back in every case, and single-instance protection.
- Multiple displays, shortcuts, dark and light themes, and an uninstall screen.
- Settings that were display-only in 0.1.0 now really work.

> **Upgrading from v0.1.0:** v0.1.0 had no automatic update check. Install this release with Settings › General › "Check now" or with the new installer; later releases arrive automatically.

## [0.1.0] - 2026-11-04

Highlights:
- First public release of Koy: a spring-animated notch and a dock for Windows 11 in a single process, with no browser engine.
- Notch panels for music, calendar and Pomodoro, shelf, control center and status, plus volume and brightness indicators.
- A floating capsule or full-width dock with window proximity modes and live window previews.
- A separate notch and dock on each display, an 8-page settings window, a setup wizard, and English and Turkish.

[0.1.1]: https://github.com/koyhq/koy/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/koyhq/koy/releases/tag/v0.1.0
