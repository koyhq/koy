# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project follows [Semantic Versioning](https://semver.org/).

Entries up to 0.2.2 are English summaries. The full history of those releases is in the Turkish changelog, [`CHANGELOG.tr.md`](CHANGELOG.tr.md).

---

## [0.2.4](https://github.com/koyhq/koy/compare/v0.2.3...v0.2.4) (2026-11-09)


### Added

* Bluetooth battery via GATT and voice command in the notch ([9baed4b](https://github.com/koyhq/koy/commit/9baed4b1af5511ed44df0765c93bb2d40e01f0b2))
* **bluetooth:** show battery for BLE devices that Windows reports without it ([44e731d](https://github.com/koyhq/koy/commit/44e731d174d61c1984783f982413386d6f2bc7f0)), closes [#50](https://github.com/koyhq/koy/issues/50)
* **dock:** add a Koy-styled Start right-click menu ([2519c4e](https://github.com/koyhq/koy/commit/2519c4e5e3bf4da9ff2585a954ba4dd66937b27d))
* **dock:** let users choose the Start button icon ([7f80337](https://github.com/koyhq/koy/commit/7f8033706282ff45702310cf6d1b93c04bd9887f))
* **notch:** add synced lyrics to the large music card ([6f3f7a2](https://github.com/koyhq/koy/commit/6f3f7a25e862d85c6a83009c65003572a30552ff))
* **notch:** open the notch on hover by default ([d564720](https://github.com/koyhq/koy/commit/d564720143cb8aeda136ec759596dc232e445ed6))
* **notch:** run a coding assistant from a voice command ([aa91b68](https://github.com/koyhq/koy/commit/aa91b6813a166b3a811ad2be2f36ad73f6e6dcfd)), closes [#48](https://github.com/koyhq/koy/issues/48)
* **notch:** show lyrics directly on the Home music card ([5c85e05](https://github.com/koyhq/koy/commit/5c85e054e7b9eaddf008cdc7f1f9231d74bda37b))
* **notch:** show synced lyrics on the Home music card ([ca08b64](https://github.com/koyhq/koy/commit/ca08b64ad251f5eeac10726dd29f5467b9ba8279))
* **notch:** turn lyrics off by default with a button on the music card ([9f8b123](https://github.com/koyhq/koy/commit/9f8b12383f0531e707c37f0363c3c11632848f06))
* **shortcuts:** allow any key and override Windows shortcuts ([a07507c](https://github.com/koyhq/koy/commit/a07507c353b6101cbe28105e8fa47e7413c4da76)), closes [#47](https://github.com/koyhq/koy/issues/47)


### Fixed

* **dock:** refresh the custom icon label when the language changes ([4ac48f8](https://github.com/koyhq/koy/commit/4ac48f81d65f874c884ab40bdeb6e4d258a6125c))

## [0.2.3](https://github.com/koyhq/koy/compare/v0.2.2...v0.2.3) (2026-11-08)

Highlights:
- **Search in the notch:** press Win+Shift+Q to search apps, open windows, files, settings, bookmarks and the web, do quick math, or close a running app, all from the notch. Alt+Shift+F2 opens it with the clipboard text.
- **English everywhere:** Koy now speaks English as well as Turkish and picks the Windows language on first run. "What's New" follows the app language, and the website and repository open in English.
- **Dock icon alignment:** on the full-width dock, app icons can sit on the left, in the center or on the right.
- **Safer search:** text from the clipboard or with several lines asks before it runs as a command, closing an app is the default and force-quitting asks first, and search history is off unless you turn it on.


### Added

* **notch:** add search panel inside the notch ([#40](https://github.com/koyhq/koy/issues/40)) ([14c3939](https://github.com/koyhq/koy/commit/14c39392fa76530b177ee2d936c9d5efa9d450ac))
* **i18n:** complete English support and follow the system language on first run ([#46](https://github.com/koyhq/koy/issues/46)) ([58e794a](https://github.com/koyhq/koy/commit/58e794a625c2e8b087b696dba274813879f32cb2)), closes [#37](https://github.com/koyhq/koy/issues/37)
* **dock:** add icon alignment option for the full-width dock ([#41](https://github.com/koyhq/koy/issues/41)) ([f4942df](https://github.com/koyhq/koy/commit/f4942df28ec6d3c7a53139edcaab7c943fe4a8c8))
* **site:** add English and a language switch, default to English ([#45](https://github.com/koyhq/koy/issues/45)) ([276acfe](https://github.com/koyhq/koy/commit/276acfe7e904115c96a528679d30c33610a0f21e)), closes [#35](https://github.com/koyhq/koy/issues/35)


### Fixed

* **search:** ask before running clipboard or multi-line text as a command ([bc47c75](https://github.com/koyhq/koy/commit/bc47c751b1f56631fbf52bbb46d888744d139d9f))
* **search:** make graceful close the default process action and verify process identity ([233cbb3](https://github.com/koyhq/koy/commit/233cbb3f68278afadf80d9c0a14369124845bbe2))
* **search:** turn history off by default and never store multi-line or clipboard text ([c494301](https://github.com/koyhq/koy/commit/c4943018693dbeff3623d54c1ba92f172995f953))
* **search:** guard file search state with a lock and dispose its cancellation sources ([d71a820](https://github.com/koyhq/koy/commit/d71a820d409e4ffdf30fc408ef7e3a7eec176ba9))
* **i18n:** refresh computed settings text when the language changes ([5759104](https://github.com/koyhq/koy/commit/5759104d7f4b9c4ff17a8930d4b5aac53a9d1a35))


### Performance

* **search:** cache PATH lookups per panel session and skip network directories ([4d201e6](https://github.com/koyhq/koy/commit/4d201e6af8ec4b3a9d053d27f57d237a5e794337))
* **search:** reuse parsed bookmarks until the file changes ([728b2e3](https://github.com/koyhq/koy/commit/728b2e300f2cf8574a020c8efbb32c57d8d7384d))

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
