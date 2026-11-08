## Summary
<!-- Briefly describe the problem this PR solves or the feature it adds. -->

## Related Issue
Closes #

## Architecture and Performance Checklist
Please verify these rules (`AGENTS.md`) before submitting the PR:

- [ ] **Layer Boundaries:** No WPF, Win32 or WinRT type was added to the `Core` layer (`App -> Platform -> Core`).
- [ ] **CsWin32:** New Win32 API calls are generated through `src/Platform/NativeMethods.txt`, without hand-written `[DllImport]`.
- [ ] **Zero Idle CPU:** Animations use `Core/Animation/Spring`; the `CompositionTarget.Rendering` subscription is detached when motion ends. No timers run in hidden panels.
- [ ] **Taskbar Safety:** If the Dock changed, the Windows taskbar comes back correctly after the app is force-killed and relaunched.
- [ ] **Localization & Vector Icons:** User-visible strings were added to both `Strings.resx` (EN) and `Strings.tr.resx` (TR). No plain Unicode emoji (SVG `Path` only).
- [ ] **Build and Tests:** `dotnet build Koy.slnx -c Release -warnaserror` (0 warnings, 0 errors) and `dotnet test Koy.slnx` are fully green.
- [ ] **Memory Measurement:** Memory impact was measured with `scripts/measure-memory.ps1` (`Private Bytes` noted).
