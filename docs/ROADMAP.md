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
- [ ] Global exception handlers (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) that log and restore windows and icons
- [ ] Null-safe, idempotent `SceneManager.Stop()`
- [ ] Restore the original desktop-icon state on exit, not "always show"
- [ ] Single-instance mutex
- [ ] Tray "Pause / Restore all windows" plus a global hotkey

## Phase 2: Correctness and threading (`fix/threading`)
- [ ] Move `OverlapCheck` to the dispatcher, or snapshot scenes under a lock
- [ ] Use one global mouse hook (SharpHook's keyboard hook is already gone)
- [ ] Recover from Explorer restarts (`TaskbarCreated`, re-resolve `WorkerW`)
- [ ] Deliberately skip elevated windows (UIPI)
- [ ] Small bugs: `AddInitialScenes` off-by-one, `SwitchToSceneByNewWindow` group key, `async void` handlers
- [ ] Mouse-hook coordinates are physical pixels but `_lastWidth` is in DIPs

## Phase 3: Fit daily workflow
- [ ] `feat/settings`: JSON settings in `%APPDATA%\StageManager` (max scenes, edge width, ignore lists, strategy, grouping), plus rolling log in `%LOCALAPPDATA%`
- [ ] `feat/hide-strategy-cloak`: hide without minimizing (keeps the taskbar and Win+Tab clean)
- [ ] `feat/multi-app-sets`: user-built sets that persist; per-app "all windows / one at a time"
- [ ] `feat/add-to-stage-gesture`: Shift+click or drag to add a window to the current stage
- [ ] `feat/multi-monitor`: per-monitor strip and stage
- [ ] `feat/virtual-desktops`

## Phase 4: Maintainability (`test/scene-logic`)
- [ ] Test project with fake `IWindow`/`IWindowsManager` covering grouping and switching
- [ ] `dotnet test` in CI
- [ ] Optional: code-sign the release exe
