# Stage Manager for Windows

This is a fork of the unmaintained [Stage Manager for Windows](https://github.com/awaescher/StageManager) project.

This is intended for personal use and reference. I am not a `C#` developer and the changes have been made entirely by Claude Opus 5.5 with little direction or oversight from me.

![Stage Manager](media/StageManager%20Basics.gif)

## Usage

 - clone this repository
 - cd into the repository directory
 - run `dotnet run --project StageManager`

To quit, find the app's tray icon (Windows might move it into the overflow menu) and use its context menu to close the app.

 - **Click** a scene in the sidebar to switch to it.
 - **Shift+click** a scene to add its windows to the current stage.
 - **Drag** a thumbnail out of the sidebar to pull that window onto the stage.
 - **Click the wallpaper** for the desktop view: every window is hidden and the desktop icons show.
 - **Ctrl+Alt+Shift+P** (or *Pause* in the tray menu) pauses Stage Manager and restores everything; press it again to resume.

### Settings

On first run the app writes its defaults to `%APPDATA%\StageManager\settings.json` (tray menu → *Settings* opens it). Changes apply after a restart; if the file can't be read, the app logs why and uses the defaults.

| Setting | Default | Meaning |
|-|-|-|
| `MaxScenes` | `6` | Scenes shown in the sidebar |
| `FlyInEdgeWidth` | `44` | Width (at 100% scaling) of the left screen strip that brings the hidden sidebar back |
| `PauseHotkey` | `"Ctrl+Alt+Shift+P"` | Pause/resume hotkey: modifiers (`Ctrl`, `Alt`, `Shift`, `Win`) plus a WPF key name (`P`, `F9`, `D1`). `""` disables it |
| `HideStrategy` | `"Minimize"` | How other scenes' windows are hidden. `"OffScreen"` parks them just below the screen instead: no minimize animation, live sidebar thumbnails, and maximized or snapped sizes are kept. Either way they stay in the taskbar and Alt+Tab |
| `PerMonitorStages` | `false` | Switching scenes only hides windows on the monitor(s) the new scene is on, so each monitor keeps its own stage. The sidebar stays on the primary monitor |
| `EnableWindowPullToScene` | `true` | Drag a thumbnail out of the sidebar to add that window to the current scene |
| `EnableWindowDropToScene` | `false` | Drag a window onto a sidebar scene to move it there (experimental) |
| `IgnoredProcesses` | `[]` | Process names (no `.exe`) to never manage, e.g. `["KeePassXC"]` |
| `IgnoredWindowClasses` | `[]` | Window classes to never manage |
| `AppGroups` | `{}` | Put several apps in one scene, e.g. `{"msedge": "Browsers", "chrome": "Browsers"}` |
| `OneWindowPerScene` | `false` | Give every window its own scene instead of grouping windows by app |
| `SeparateWindowApps` | `[]` | Apps whose windows each get their own scene, e.g. `["Teams", "OUTLOOK"]` |

**Virtual desktops:** each desktop has its own stage. The sidebar shows the current desktop's scenes, and switching scenes never touches windows on other desktops.

**App sets:** when you move every window of an app into another scene (by dragging, or Shift+clicking a scene to add it to the current one), Stage Manager remembers it. New windows of that app open in that scene, and the set is rebuilt after a restart. The tray's *Forget app sets* undoes this. Learned sets live in `%LOCALAPPDATA%\StageManager\groups.json`; `AppGroups` in the settings always wins.

Logs are in `%LOCALAPPDATA%\StageManager\stagemanager.log` (the previous one is kept as `stagemanager.1.log` once it passes 1 MB).

### Requirements
 - Windows 10 version 2004 or newer
 - [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download) or newer

## Roadmap

What's done and what's next is tracked in [docs/ROADMAP.md](docs/ROADMAP.md).

---

## License

MIT, see [LICENSE](LICENSE). Code adapted from other projects keeps its original license; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
