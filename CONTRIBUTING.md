# Contributing to Koy

Thank you for your interest in contributing to **Koy**! This document covers the code architecture, open source development rules, coding standards and the PR process.

---

## 1. Architecture and Layering Rules

Koy is a clean three-layer architecture running in a single process:

```text
src/
  Core/         Pure C# (net10.0): absolutely no WPF, Win32 or WinRT
  Platform/     Windows API implementations (CsWin32 & WinRT): no WPF
  App/          WPF UI, thin ViewModels (CommunityToolkit.Mvvm) and XAML views
tests/
  Core.Tests/   xUnit unit tests (covers the Core layer only)
```

### Architecture Principles
1. **Dependency Direction:** `App → Platform → Core` and `App → Core`. `Core` must not depend on any outer layer or operating system API.
2. **Contracts Live in Core:** The interface (`IMediaService`, `IAudioService`, `IMonitorService`, etc.) and data model of every platform service are defined in `src/Core/<Area>/`.
   - **WinRT and Win32 types must not appear** in `Core` interfaces (`HWND`, `HMONITOR` and `IRandomAccessStreamReference` are forbidden).
   - Window and display handles are passed as `nint`, images as `byte[]`.
3. **CsWin32:** Do not write `[DllImport]` or `[LibraryImport]` by hand. Add the name of the Win32 API or COM interface to `src/Platform/NativeMethods.txt`; the `Microsoft.Windows.CsWin32` source generator produces type-safe P/Invoke code.
4. **Feature Entry Point (`IFeature`):** Every main feature (`NotchFeature`, `DockFeature`) implements `IFeature` (`Start` / `Stop`). A disabled feature creates no window, background service or WinEvent hook (`SetWinEventHook`).
5. **No Monolithic Files:** Do not pile business logic, P/Invoke and UI logic into one file. Sub-pages (`Pages/`), windows and `Converters` stay modular in separate files.

---

## 2. Performance and Memory Discipline

What sets Koy apart from Electron or browser-based alternatives (Tauri/WebView2/Electron) is that it is **lightweight**:

1. **No Browser:** WebView2, CefSharp, Electron and Blazor Hybrid are never used.
2. **No Heavy Libraries:** Heavy NuGet packages such as Serilog, Autofac, ReactiveUI and MahApps are not added. Settings serialization uses the source-generated `SettingsJsonContext` instead of reflection-based JSON.
3. **Software Rendering (`RenderMode.SoftwareOnly`):** Software rendering is the default, to avoid the ~135 MB that GPU drivers add to the process.
4. **Zero Idle CPU:**
   - The `Koy.Core.Animation.Spring` physics engine is used instead of WPF `Storyboard`.
   - `CompositionTarget.Rendering` is attached only when an animation starts and detached as soon as the spring settles (`Spring.IsAtRest`).
   - `DispatcherTimer` does not run in hidden panels; Control Center and Status services are started lazily when the panel is first opened.
5. **Image Optimization:** Album art and app icons are decoded at display size (`DecodePixelWidth`) and immediately `.Freeze()`d.
6. **Fixed Canvas Rule:** The notch window (`NotchWindow`) does not change `Window.Width/Height` during animation (this flickers on transparent windows); the `NotchShape` geometry grows and shrinks inside a fixed canvas.

---

## 3. Taskbar Safety (Critical Rule)

When the Dock is enabled, Koy hides the Windows taskbar (`Shell_TrayWnd` and `Shell_SecondaryTrayWnd` on secondary displays).
- However the app exits (normal exit, unhandled exception, update, uninstall, or relaunch after being force-killed from Task Manager), **the Windows taskbar must come back** (`TaskbarService.Restore()`).
- **When the Dock is disabled (`DockEnabled = false`)**, the Windows taskbar is never touched, except for the safety `Restore()` call at startup.
- Perform this manual test in every Dock-related PR:
  1. While Koy is running, kill the `Koy.exe` process from Task Manager.
  2. Restart Koy or press the **"Restore Windows taskbar"** button on the About page.
  3. Verify that the taskbar is fully back.

---

## 4. Code Style and UI Rules

- **C# Standards:** File-scoped namespaces (`namespace Koy.Core.Notch;`), `<Nullable>enable</Nullable>`, private fields as `_camelCase`.
- **MVVM:** ViewModels use the `CommunityToolkit.Mvvm` source generators (`[ObservableProperty]`, `[RelayCommand]`).
- **No Plain Emoji:** Plain Unicode emoji (`🚀`, `🎵`, `⚙️`, etc.) are **never** used in the UI. All icons are clean vector `Path` (`Geometry`) definitions in `Styles.xaml` or the relevant XAML.
- **No Dead Buttons:** Do not add fake buttons or shortcuts that do nothing.
- **Localization (EN / TR):** No user-visible text is hard-coded in XAML or C#. Every new string goes into both `src/App/Resources/Strings.resx` (English) and `src/App/Resources/Strings.tr.resx` (Turkish).

---

## 5. Development Environment and Commands

### Requirements
- Windows 10 (2004 / build 19041 or later) or Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Build, Test and Package
```powershell
# Build (Release, zero warnings)
dotnet build Koy.slnx -c Release -warnaserror

# Run unit tests (xUnit)
dotnet test Koy.slnx -c Release

# Run the app
dotnet run --project src/App

# Measure memory usage (Private Bytes)
powershell -ExecutionPolicy Bypass -File scripts\measure-memory.ps1

# Build the Velopack installer and portable ZIP (unsigned by default)
powershell -ExecutionPolicy Bypass -File scripts\pack.ps1 -Version 0.1.0

# Optional: sign Koy.exe, Koy.Watchdog.exe and Koy-win-Setup.exe with a PFX certificate
powershell -ExecutionPolicy Bypass -File scripts\pack.ps1 -Version 0.1.0 -CertFile .\cert.pfx -CertPassword "secret"
# Or pass custom signtool arguments directly (also via KOY_SIGN_PARAMS / KOY_SIGN_CERT_FILE / KOY_SIGN_CERT_PASSWORD):
powershell -ExecutionPolicy Bypass -File scripts\pack.ps1 -Version 0.1.0 -SignParams '/fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /n "My Subject"'
```

---

## 6. Git Flow and Pull Requests

1. **Branch Names:**
   - New features: `feat/short-description`
   - Bug fixes: `fix/short-description`
   - Performance improvements: `perf/short-description`
2. **Commit Messages:** Written in **English**, in the imperative mood, with Conventional prefixes (`feat:`, `fix:`, `perf:`, `refactor:`, `docs:`, `test:`), for example `fix(dock): keep taskbar hidden on sleep`.
3. **Tests Required:** Every state machine, layout calculator or rule added to `Core` must have an xUnit test under `tests/Core.Tests/`.
4. **License:** By submitting a contribution you agree that it is published under the project's [GPLv3](LICENSE) license.

---

## 7. How Releases Work

Releases are prepared automatically with [release-please](https://github.com/googleapis/release-please); tags are never created by hand.

1. **Commit prefixes determine the release** (based on the commits merged into `main`):
   - `fix:` → patch release, listed under fixes in the changelog.
   - `perf:` → patch release, listed under performance.
   - `!` or `BREAKING CHANGE:` (e.g. `fix!:`) → minor release before 1.0.
   - `docs:`, `refactor:`, `test:`, `chore:` and `ci:` do not appear in the changelog and do not trigger a release on their own.
2. On every merge to `main`, release-please opens or updates a **release PR** (`CHANGELOG.md` and the version number in `Directory.Build.props`).
3. When ready to publish, merge that PR. Nothing else is needed.
4. After the merge, the tag (`vX.Y.Z`) and GitHub release are created automatically, and the same workflow attaches the installer, the portable zip and `SHA256SUMS.txt` to the release.

You can edit the release PR's text (`CHANGELOG.md`) before merging. The hand-written `[Unreleased]` section in `CHANGELOG.md` describes the same changes as the entry generated by release-please: in the release PR, delete the `[Unreleased]` heading and move its content under the new version heading (release-please only lists `fix:`/`perf:` lines, so user-facing work with a `refactor:` prefix does not appear in the changelog). To repackage a tag, run the **Release** workflow manually (`workflow_dispatch`) from the Actions tab with the tag name.

### Optional Code Signing in Releases
The `Release` workflow (`.github/workflows/release.yml`) supports optional Authenticode signing:
- Configure the repository secrets `SIGNING_CERT_BASE64` (base64-encoded `.pfx` certificate) and `SIGNING_CERT_PASSWORD` (optional if the certificate has no password).
- When `SIGNING_CERT_BASE64` is set, the workflow decodes the certificate to a temporary file on the runner, signs `Koy.exe` and `Koy.Watchdog.exe` via `signtool.exe` (with `http://timestamp.digicert.com`), passes `--signParams` to `vpk pack` so `Koy-win-Setup.exe` and the `.nupkg` payloads are signed, and deletes the temporary `.pfx` afterwards.
- When `SIGNING_CERT_BASE64` is not set, the signing steps are skipped and the unsigned release flow runs as usual.
- SignPath Foundation or Azure Trusted Signing (`vpk pack --azureTrustedSignFile`) can also be integrated into `.github/workflows/release.yml` in place of a PFX secret.
