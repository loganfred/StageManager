# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project
WPF tray app (.NET 8, `net8.0-windows`, WPF + WinForms enabled) that mimics macOS Stage Manager on Windows 10 2004+. Fork of awaescher/StageManager (upstream is unmaintained); window-tracking code under `StageManager/Native/` was adapted from workspacer. Planned work and status live in `docs/ROADMAP.md`.

## Commands
- Build: `dotnet build StageManager.sln`
- Run: `dotnet run --project StageManager`. Quit via the tray icon's context menu → Quit. Only one instance can run (named mutex). Killing the process skips cleanup and leaves other windows minimized; the original desktop-icon state is remembered in `session.txt` and restored by the next clean exit.
- Logs: `%LOCALAPPDATA%\StageManager\stagemanager.log` (`Diagnostics/Log`; rolls over to `stagemanager.1.log` at 1 MB).
- Settings: `%APPDATA%\StageManager\settings.json`, created with defaults on first run and documented in `ReadMe.md`.
- Release single-file exe (what CI does; CI only publishes a GitHub release on a manual `workflow_dispatch` run): `dotnet publish StageManager -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -o Artifacts`
- Test: `dotnet test StageManager.sln` (xunit, `StageManager.Tests`). One test: `dotnet test StageManager.Tests --filter "FullyQualifiedName~Desktop_view"`. CI runs the tests on every push.
- There is no linter. The build has ~100 pre-existing nullable warnings; don't add new ones.

## Architecture
- `App` creates `MainWindow` in `OnStartup` (no `StartupUri`) after taking the single-instance mutex. Its unhandled-exception handlers call `SceneManager.Active?.Stop()` to put the desktop back. Any code that hides windows or changes desktop icons must stay undoable through `Stop()`, which is idempotent and callable from any thread.
- `Settings` is loaded once in `App.OnStartup` and read through the static `Settings.Current`; there is no live reload. New tunables belong there (with a sane default and a check in `Normalized()`), plus a row in the ReadMe table.
- `MainWindow` (sidebar UI + tray icon) owns everything. `OnContentRendered` creates `WindowsManager` → `SceneManager`, builds the initial `SceneModel`s, and switches to the foreground window's scene. `OnClosed` calls `SceneManager.Stop()` (re-shows all windows, restores desktop icons), then `Environment.Exit(0)`.
- `Native/WindowsManager` enumerates top-level windows at start (`EnumWindows` + `Win32Helper.IsAppWindow`), then tracks them through `SetWinEventHook` callbacks (show/destroy/cloak/minimize/foreground/move/location). Those callbacks arrive on the thread that called `Start`, which is why `SceneManager.Start` asserts it runs on the main thread. It emits `WindowCreated/Destroyed/Updated` and `UntrackedFocus`; the latter is how a click on the desktop `WorkerW` is detected.
- `Native/WindowsWindow` is the `IWindow` wrapper over an HWND. `IsCandidate()` holds the built-in class/process ignore lists (plus the user's from settings) that decide which windows get managed.
- `SceneManager` receives everything it touches as interfaces (`IWindowsManager`, `IDesktop`, `IWindowStrategy`, `Settings`), so `StageManager.Tests` drives it with fakes (`Fakes.cs`: `Harness`, `FakeWindowsManager.Open/Activate/Close`). Behavior changes to scene logic should come with a test there. `ReentrancyWindow` is set to zero in tests.
- `SceneManager`: a `Scene` is a group of windows keyed by `GetWindowGroupKey` (process name, or its `AppGroups` group). `SwitchTo(scene)` shows that scene's windows and hides all others through the `IWindowStrategy` picked by the `HideStrategy` setting: `NormalizeAndMinimizeWindowStrategy` (default) or `OffScreenWindowStrategy`, which parks windows below the virtual screen and journals them to `parked.txt` so `RecoverFromUncleanExit` can move them back after a killed session. `RestoreAll()` must undo every hide still in effect, including windows that have left all scenes. `_suspend` suppresses re-entrant foreground events while switching. `IsReentrancy` blocks bouncing back to a just-hidden scene for 1s (e.g. Teams' floating call window). `SwitchTo(null)` is "desktop view" and shows the desktop icons.
- UI: `Model/SceneModel` and `WindowModel` are view models, and `DwmThumbnail` renders live DWM thumbnails in the sidebar. `MainWindow` slides off-screen when a current window overlaps the sidebar (500ms `OverlapCheck` `DispatcherTimer`) and flies back in when the mouse reaches the left `FlyInEdgeWidth` DIPs. Mouse-hook and `GetWindowRect` coordinates are physical pixels, so compare them with `_sidebarWidthPx`/`_flyInEdgePx`, not `Width`.
- Pause: tray "Pause" or the `PauseHotkey` (default Ctrl+Alt+Shift+P) (`RegisterHotKey` + `WndProc` hook in `MainWindow`). `SceneManager.Pause()` restores everything and ignores window events; `Resume()` re-applies the current stage through `ApplyStage` without raising selection events.
- Threading: WinEvent callbacks, the overlap timer and UI handlers all run on the UI thread. Only the SharpHook callbacks run on the thread pool, and they marshal to the dispatcher. Explorer restarts arrive as `TaskbarCreated` in `MainWindow.WndProc`.
- Input: the only global hook (SharpHook `TaskPoolGlobalHook`, run with `GlobalHookType.Mouse` so no keyboard hook is installed) in `MainWindow` handles dragging a window out of a sidebar scene into the current one (`EnableWindowPullToScene`). The hook is stopped while the tray context menu is open.
- Side effects that must stay reversible:
  - windows are minimized via `ShowWindow` or parked off-screen via `SetWindowPos` (async, so a hung app can't block the UI thread);
  - desktop icons are toggled by sending Explorer `WM_COMMAND 0x7402` to `SHELLDLL_DefView`, which persists to the registry;
  - autostart writes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

## Upstream PRs
Upstream PRs are mirrored locally as `upstream-pr/<n>`. PR #34 was reviewed and **not merged**: it removes window hiding, per-process grouping and desktop view. Only its isolated fixes were ported (see `docs/ROADMAP.md`).
