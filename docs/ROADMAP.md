# Roadmap

Goal: take this prototype to something reliable for daily use. Each item lands on its own branch and PR against `loganfred/StageManager:main`; tick the box in that PR.

## Upstream triage
- [x] PRs #9, #10, #13, #16, #20, #21 and `feature/live-thumbnails`: already in `main`
- [x] PRs #11, #12, #15: superseded workflow/packaging churn, dropped
- [x] PR #29: unrelated website, rejected
- [x] PR #33 / #34: not merged (they remove window hiding, process grouping and desktop view). Isolated fixes ported on `fix/pr34-stability`:
  - [x] CI: actions v4, .NET 8, tag/release only on push
  - [x] AutoStart null-safety
  - [x] Unhook WinEvent and mouse hooks on stop; thread-safe window map
  - [x] GDI leak in icon conversion
- [x] Delete the local mirrored `upstream-pr/*` and `upstream/feature/live-thumbnails` refs
- [ ] Delete `origin/feature/live-thumbnails` on GitHub (already in `main`)

## Phase 0: CI and dependency hygiene (`chore/ci-deps`)
- [x] Release only on manual dispatch (Actions → Run workflow); publish for `win-x64`
- [x] No vulnerable packages (`dotnet list package --vulnerable --include-transitive`)
- [x] Dropped ControlzEx; bumped SharpHook 8 (now mouse-only, no keyboard hook), Hardcodet.NotifyIcon.Wpf 2, AsyncAwaitBestPractices 10, WpfScreenHelper 2.1.1
- [x] Removed dead P/Invokes (`CreateFile`, console and system-menu APIs); kept the alternative hide strategies for the cloak work

## Phase 1: Never leave the desktop broken (`fix/crash-safe-restore`)
- [x] Global exception handlers (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) that log and restore windows and icons
- [x] Null-safe, idempotent `SceneManager.Stop()`
- [x] Restore the original desktop-icon state on exit, not "always show" (kept in `session.txt`, so it survives a killed session)
- [x] Single-instance mutex
- [x] Tray "Pause" (shows all windows) plus the global hotkey Ctrl+Alt+Shift+P

## Phase 2: Correctness and threading (`fix/threading`)
- [x] `OverlapCheck` runs on a `DispatcherTimer` (UI thread)
- [x] One global mouse hook: removed `WindowsManager`'s unused `WH_MOUSE_LL` hook and its thread
- [x] Recover from Explorer restarts (`TaskbarCreated`, re-resolve `WorkerW`)
- [x] Elevated/protected windows are skipped on purpose (documented in `WindowsWindow`)
- [x] Small bugs: `SwitchToSceneByNewWindow` group key, `async void` handler, leaked `Process` handles, swallowed `SafeFireAndForget` exceptions. (`AddInitialScenes`' `i <= MAX_SCENES` is intentional; see its comment.)
- [x] Sidebar hit tests use physical pixels (`_sidebarWidthPx`)

## Phase 3: Fit daily workflow
- [x] `feat/settings`: JSON settings in `%APPDATA%\StageManager` (max scenes, edge width, hotkey, drag options, ignore lists, app groups), plus rolling log in `%LOCALAPPDATA%`. The hide-strategy setting comes with `feat/hide-strategy-cloak`, since minimizing is the only strategy today
- [x] `feat/hide-strategy`: `HideStrategy: "OffScreen"` parks windows below the screen instead of minimizing. True DWM cloaking isn't possible for other processes' windows without undocumented, build-specific COM interfaces, so parked windows still show in the taskbar and Alt+Tab
- [x] `feat/multi-app-sets`: sets you build by moving an app's windows are remembered (`groups.json`) and rebuilt after a restart; `OneWindowPerScene` and `SeparateWindowApps` for "one at a time"
- [x] `feat/add-to-stage-gesture`: Shift+click adds a scene to the current stage; dragging a thumbnail out pulls that window (not just the scene's last one)
- [x] `feat/multi-monitor`: `PerMonitorStages` keeps a stage per monitor; sidebar overlap, fly-in and slide-out are now correct with monitors beside the primary. The sidebar itself stays on the primary monitor (a strip per monitor would need one sidebar window per monitor)
- [ ] `feat/virtual-desktops`

## Phase 4: Maintainability (`test/scene-logic`)
- [x] Test project with fake `IWindow`/`IWindowsManager` covering grouping and switching
- [x] `dotnet test` in CI
- [ ] Optional: code-sign the release exe
