# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project follows [Semantic Versioning](https://semver.org/).

Entries up to 0.2.2 are English summaries. The full history of those releases is in the Turkish changelog, [`CHANGELOG.tr.md`](CHANGELOG.tr.md).

---

## [0.2.6](https://github.com/koyhq/koy/compare/v0.2.5...v0.2.6) (2026-11-11)


### Added

* **appearance:** add frosted and glass material levels ([e4aad69](https://github.com/koyhq/koy/commit/e4aad69f5e398d1d2383005a5945482a570dea87))
* **appearance:** give the tray menu and setup wizard the system acrylic frame ([f1705f1](https://github.com/koyhq/koy/commit/f1705f19cbb6e22ce470123a4309ddfcf7630d27))
* **appearance:** use the system acrylic backdrop for the Koy window ([75b3ac9](https://github.com/koyhq/koy/commit/75b3ac940ff538aeb43c5b3d37424c556b622f04))
* glass material, notch cards and notifications, floating island, album colors ([182d7ae](https://github.com/koyhq/koy/commit/182d7aeea74adc77fb32fc6a5eef8f065e751672))
* **media:** add the music card style setting and a cover fingerprint ([857e99a](https://github.com/koyhq/koy/commit/857e99a1c01a6fc6d04a239d182e4ae3ad47bb5d))
* **media:** extract an accent palette from album artwork ([fc2c492](https://github.com/koyhq/koy/commit/fc2c4921cf532f94e70ac24feb78dfb3d1763a9a))
* **music:** follow the glass material on the music card ([333a801](https://github.com/koyhq/koy/commit/333a801a08bc3e87dbb9ad265e4dbd985b2ecfe0))
* **music:** tint the music card and use the album artwork as its backdrop ([e7b9f6f](https://github.com/koyhq/koy/commit/e7b9f6faf0e04a0b0200c879b79068fa941a8ad2))
* **music:** use neutral tones for the plain music card ([f0f44a1](https://github.com/koyhq/koy/commit/f0f44a1e8805652d1f2f03052166f2758e5344e8))
* **notch:** add a floating island style ([f528d5a](https://github.com/koyhq/koy/commit/f528d5a7bdeba14c6afd06602afdcd28db78b02f))
* **notch:** add a ring battery and day-time idle display ([70ab63e](https://github.com/koyhq/koy/commit/70ab63e0670855b3855bf655fe2ede19802da027))
* **notch:** show an update card on the notch when a new release is available ([887b95a](https://github.com/koyhq/koy/commit/887b95ab1d4f01280ec0029ef57de8320092e4a7))
* **notch:** show the Do Not Disturb state on its control tile ([611af88](https://github.com/koyhq/koy/commit/611af885f3933ef2cad9192941236063a8beada5))
* **notch:** show Windows notifications as cards on the notch ([09f3217](https://github.com/koyhq/koy/commit/09f32178726af9206de60a810de04352699942df))
* **notifications:** add notification diff, poller, queue and card presentation ([62d5455](https://github.com/koyhq/koy/commit/62d545545fabc72f2db76f187fdc8bd46777f854))
* **notifications:** poll the Windows notification list without package identity ([08bbe41](https://github.com/koyhq/koy/commit/08bbe4188ed78752e662fc926d553dfc49a2b1e1))
* **voice:** add Dictation tab with Claude permission mode, model choice and reply card ([dee5f8b](https://github.com/koyhq/koy/commit/dee5f8be3a1c9fda2b6c58778f85fa355e304d48))
* **voice:** Dictation tab with Claude permission mode, model choice and reply card ([0e32d97](https://github.com/koyhq/koy/commit/0e32d97226cb103aaffa29ef2e10cd9f16a3a78c))


### Fixed

* **dock:** close the window when its preview is middle-clicked ([66d4bc0](https://github.com/koyhq/koy/commit/66d4bc05146b50981cca15e1bccea8308f00f0b5))
* **notch:** crop transparent margins so notification logos fill the icon circle ([b243c5d](https://github.com/koyhq/koy/commit/b243c5d6e12104d701de094d17e9f51617f74986))
* **notch:** keep the notch visible while a card is shown ([2ca6ea1](https://github.com/koyhq/koy/commit/2ca6ea12909a830b4c8bc5c71a3a287270d2ad6e))
* **notch:** show the right app icon on notification cards ([6c72ad6](https://github.com/koyhq/koy/commit/6c72ad61c1cd0e5e6b1831a8fd77e48fc64afaa9))
* **notifications:** detect Do Not Disturb and focus sessions on Windows 11 ([123f4c0](https://github.com/koyhq/koy/commit/123f4c045de48894d03a690efdad5132ffd06b77))
* **notifications:** drop the unreliable registry check for Do Not Disturb ([b0fdc4e](https://github.com/koyhq/koy/commit/b0fdc4ea8fbe229831fa791891aacde50dc8f083))
* **notifications:** treat a replaced notification with the same id as new ([2cae303](https://github.com/koyhq/koy/commit/2cae303041fcc857b71bad474226a7e6349c4279))
* **voice:** default to read-only permission, localize model list and invalid model error ([980eb09](https://github.com/koyhq/koy/commit/980eb098c113e163642b44b585cc4ab7fc9d6cde))

## [0.2.5](https://github.com/koyhq/koy/compare/v0.2.4...v0.2.5) (2026-11-09)
Highlights:
- **Voice command in Turkish and English:** Ctrl+Alt+V now opens a box in the notch and starts Windows voice typing (Win+H), which writes in your keyboard language, so Turkish works without installing a speech language. You can also type the command. Enter runs it, Esc cancels, and the first line of the reply shows in the notch; click it for the full reply.
- **Web search in your browser:** when nothing matches, the search opens your default browser and searches with its own engine (Chrome, Edge, Brave, Vivaldi, Firefox and others) instead of DuckDuckGo.
- **Clearer errors:** if the coding tool fails, the notch shows its own message, for example "Not logged in", instead of just an exit code.



### Fixed

* **notch:** show the voice command reply and keep Enter working ([afacb55](https://github.com/koyhq/koy/commit/afacb55aea7af8c125590ffa73a4fdafbc10f376))
* **notch:** use Windows voice typing so voice commands work in Turkish and English ([3078a72](https://github.com/koyhq/koy/commit/3078a72a44f8f4d9c33ecccba81b3a9b25f1df41))
* **search:** search the web in the default browser with its own engine ([7063934](https://github.com/koyhq/koy/commit/70639341f0ec69000e4b4cf7e6cf71d3ec52690d))

## [0.2.4](https://github.com/koyhq/koy/compare/v0.2.3...v0.2.4) (2026-11-09)

Highlights:
- **Voice command in the notch:** press Ctrl+Alt+V, speak, check the text in the notch and press Enter to run Claude Code, Codex, Antigravity or your own command. No shell is used, and Esc cancels. Dictation uses Windows online speech recognition, so audio goes to Microsoft only while Koy is listening.
- **Song lyrics:** the Home music card shows synced lyrics for the playing track, and clicking a line jumps there. Lyrics are off by default; turn them on with the small button on the music card (the track title is then looked up on LRCLIB, and .lrc files in Music\Lyrics are used first).
- **Bluetooth battery:** BLE keyboards, mice and headsets that Windows shows without a battery level now show one, without jumps while typing or when lights change.
- **Start button:** pick the Start icon on the dock, and right-click it for a Koy-styled Win+X menu.
- **Shortcuts:** any key can be a shortcut and can replace a Windows shortcut. The notch now opens on hover by default.


### Added

* **bluetooth:** show battery for BLE devices that Windows reports without it ([44e731d](https://github.com/koyhq/koy/commit/44e731d174d61c1984783f982413386d6f2bc7f0)), closes [#50](https://github.com/koyhq/koy/issues/50)
* **dock:** add a Koy-styled Start right-click menu ([2519c4e](https://github.com/koyhq/koy/commit/2519c4e5e3bf4da9ff2585a954ba4dd66937b27d))
* **dock:** let users choose the Start button icon ([7f80337](https://github.com/koyhq/koy/commit/7f8033706282ff45702310cf6d1b93c04bd9887f))
* **notch:** add synced lyrics to the large music card ([6f3f7a2](https://github.com/koyhq/koy/commit/6f3f7a25e862d85c6a83009c65003572a30552ff))
* **notch:** open the notch on hover by default ([d564720](https://github.com/koyhq/koy/commit/d564720143cb8aeda136ec759596dc232e445ed6))
* **notch:** run a coding assistant from a voice command ([aa91b68](https://github.com/koyhq/koy/commit/aa91b6813a166b3a811ad2be2f36ad73f6e6dcfd)), closes [#48](https://github.com/koyhq/koy/issues/48)
* **notch:** show lyrics directly on the Home music card ([5c85e05](https://github.com/koyhq/koy/commit/5c85e054e7b9eaddf008cdc7f1f9231d74bda37b))
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
