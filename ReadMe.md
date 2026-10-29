# Stage Manager for Windows

This is an experimental approach to bring the macOS [Stage Manager](https://support.apple.com/en-us/HT213315) to Microsoft Windows.

> **Important:** This is a prototype and a feasibility study - I am not actively developing this project at the moment but I'd be happy to review and merge pull requests. 

![Stage Manager](media/StageManager%20Basics.gif)

This prototype groups applications by their process. By switching between so called "scenes" on the left, Stage Manager hides other windows and the desktop icons, helping you to focus.

Windows can be moved from one scene to another by dragging them onto scenes on the left.

## Usage

Download and run the executable from the [Releases tab](https://github.com/awaescher/StageManager/releases/) or 
 - clone this repository
 - cd into the repository directory
 - run `dotnet run --project StageManager`

To quit, find the app's tray icon (Windows might move it into the overflow menu) and use its context menu to close the app.

### Settings

On first run the app writes its defaults to `%APPDATA%\StageManager\settings.json` (tray menu → *Settings* opens it). Changes apply after a restart; if the file can't be read, the app logs why and uses the defaults.

| Setting | Default | Meaning |
|-|-|-|
| `MaxScenes` | `6` | Scenes shown in the sidebar |
| `FlyInEdgeWidth` | `44` | Width (at 100% scaling) of the left screen strip that brings the hidden sidebar back |
| `PauseHotkey` | `"Ctrl+Alt+Shift+P"` | Pause/resume hotkey: modifiers (`Ctrl`, `Alt`, `Shift`, `Win`) plus a WPF key name (`P`, `F9`, `D1`). `""` disables it |
| `HideStrategy` | `"Minimize"` | How other scenes' windows are hidden. `"OffScreen"` parks them just below the screen instead: no minimize animation, live sidebar thumbnails, and maximized or snapped sizes are kept. Either way they stay in the taskbar and Alt+Tab |
| `EnableWindowPullToScene` | `true` | Drag a thumbnail out of the sidebar to add that window to the current scene |
| `EnableWindowDropToScene` | `false` | Drag a window onto a sidebar scene to move it there (experimental) |
| `IgnoredProcesses` | `[]` | Process names (no `.exe`) to never manage, e.g. `["KeePassXC"]` |
| `IgnoredWindowClasses` | `[]` | Window classes to never manage |
| `AppGroups` | `{}` | Put several apps in one scene, e.g. `{"msedge": "Browsers", "chrome": "Browsers"}` |

Logs are in `%LOCALAPPDATA%\StageManager\stagemanager.log` (the previous one is kept as `stagemanager.1.log` once it passes 1 MB).
 
### Requirements
 - Windows 10 version 2004 or newer
 - [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download)

## To do

This is an experimental fun project. I don't have any idea whether or not this is going to be a final product one day. 

|Topic|State|
|-|-|
|**Experimental stage**||
|initial windows grouping by process|✅|
|3D display of opened windows (static)|✅|
|hide/show windows of given scenes|✅|
|hide/show desktop icons|✅|
|scene management with drag&drop|✅|
|restore windows on quit/restart|✅|
|auto hide & fly-in scenes for maximized windows|✅|
|full screenshots for windows that were minimized on startup|✅|
|drag windows from other scenes into the current one|✅|
|place screenshots in relative size of the desktop|⬜|
|limit maximum scenes (like 6 for macOS?)|✅|
|limit window count per scene (like newest 5)|⬜|
|tray icon to start & stop|✅|
|start with Windows|✅|
|**Product stage**||
|virtual desktop support (pin window)|⬜|
|multi-monitor support|⬜|
|visual feedback when dragging windows from other scenes|⬜|
|feature parity with macOS Stage Manager|⬜|
|**Polishing stage**||
|window animations|⬜|
|live dwm thumbnails|✅|
|adjust 3D angle according to screen position|⬜|
|flyover sidebar in desktop view mode if icons are close to the left|⬜|

Contributions very welcome :heart:

---

Stage Manager is using a few code files to handle window tracking from [workspacer](https://github.com/workspacer/workspacer), an amazing open source project by [Rick Button](https://github.com/rickbutton).
