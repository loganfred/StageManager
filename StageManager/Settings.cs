using StageManager.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;

namespace StageManager
{
	/// <summary>
	/// User settings from %APPDATA%\StageManager\settings.json. Read once at startup; edits apply after a restart.
	/// A missing file is created with the defaults; an unreadable one is logged and ignored (never overwritten).
	/// </summary>
	internal static class HideStrategies
	{
		public const string Minimize = "Minimize";
		public const string OffScreen = "OffScreen";

		public static readonly string[] All = { Minimize, OffScreen };
	}

	internal sealed class Settings
	{
		private static readonly JsonSerializerOptions _jsonOptions = new()
		{
			WriteIndented = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
		};

		public static Settings Current { get; private set; } = new Settings();

		public static string FilePath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StageManager", "settings.json");

		/// <summary>Scenes shown in the sidebar; the rest stay tracked but hidden from it.</summary>
		public int MaxScenes { get; set; } = 6;

		/// <summary>Width (in DIPs, i.e. at 100% scaling) of the left screen strip that brings the hidden sidebar back.</summary>
		public double FlyInEdgeWidth { get; set; } = 44;

		/// <summary>Global hotkey that pauses/resumes, e.g. "Ctrl+Alt+Shift+P". Empty disables it.</summary>
		public string PauseHotkey { get; set; } = "Ctrl+Alt+Shift+P";

		/// <summary>
		/// How windows outside the current scene are hidden: "Minimize" (default) or "OffScreen"
		/// (moved below the screen: no animation, live thumbnails, maximized/snapped sizes kept).
		/// </summary>
		public string HideStrategy { get; set; } = HideStrategies.Minimize;

		/// <summary>
		/// Switching scenes only hides windows on the monitor(s) the new scene is on, so every monitor keeps its own stage.
		/// </summary>
		public bool PerMonitorStages { get; set; } = false;

		/// <summary>Drag a window thumbnail out of the sidebar to add it to the current scene.</summary>
		public bool EnableWindowPullToScene { get; set; } = true;

		/// <summary>Drag a window onto a sidebar scene to move it there (experimental).</summary>
		public bool EnableWindowDropToScene { get; set; } = false;

		/// <summary>Process names (without .exe) that are never managed, in addition to the built-in shell list.</summary>
		public List<string> IgnoredProcesses { get; set; } = new();

		/// <summary>Window class names that are never managed, in addition to the built-in shell list.</summary>
		public List<string> IgnoredWindowClasses { get; set; } = new();

		/// <summary>Give every window its own scene instead of grouping by app (macOS: "one at a time").</summary>
		public bool OneWindowPerScene { get; set; } = false;

		/// <summary>Process names whose windows each get their own scene, e.g. ["Teams", "OUTLOOK"].</summary>
		public List<string> SeparateWindowApps { get; set; } = new();

		/// <summary>
		/// Process name → group name. Processes mapped to the same group share one scene,
		/// e.g. { "msedge": "Browsers", "chrome": "Browsers" }. Unlisted processes get their own scene.
		/// </summary>
		public Dictionary<string, string> AppGroups { get; set; } = new();

		public static void Load()
		{
			try
			{
				if (!File.Exists(FilePath))
				{
					Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
					File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, _jsonOptions));
					Log.Info($"Wrote default settings to {FilePath}");
					return;
				}

				Current = Parse(File.ReadAllText(FilePath));
				Log.Info($"Loaded settings from {FilePath}");
			}
			catch (Exception ex)
			{
				Current = new Settings();
				Log.Error($"Could not read {FilePath}; using defaults", ex);
			}
		}

		/// <summary>
		/// Deserializes settings JSON (comments and trailing commas allowed) and replaces out-of-range values with defaults.
		/// </summary>
		internal static Settings Parse(string json) => (JsonSerializer.Deserialize<Settings>(json, _jsonOptions) ?? new Settings()).Normalized();

		private Settings Normalized()
		{
			var defaults = new Settings();

			if (MaxScenes < 1)
				MaxScenes = defaults.MaxScenes;
			if (FlyInEdgeWidth <= 0)
				FlyInEdgeWidth = defaults.FlyInEdgeWidth;

			// JSON null leaves these null; keys compare case-insensitively like process names do
			PauseHotkey ??= "";
			HideStrategy = HideStrategies.All.FirstOrDefault(s => string.Equals(s, HideStrategy, StringComparison.OrdinalIgnoreCase)) ?? defaults.HideStrategy;
			IgnoredProcesses ??= new();
			IgnoredWindowClasses ??= new();
			SeparateWindowApps ??= new();
			AppGroups = new Dictionary<string, string>(
				(AppGroups ?? new()).Where(p => !string.IsNullOrWhiteSpace(p.Value)),
				StringComparer.OrdinalIgnoreCase);

			return this;
		}

		public bool IsSeparateWindowApp(string processName) =>
			OneWindowPerScene || SeparateWindowApps.Contains(processName, StringComparer.OrdinalIgnoreCase);

		public string GetGroupKey(string processName) => AppGroups.TryGetValue(processName, out var group) ? group : processName;

		/// <summary>
		/// Parses <see cref="PauseHotkey"/> into RegisterHotKey arguments. False when empty or malformed.
		/// </summary>
		public bool TryGetPauseHotkey(out uint modifiers, out Key key)
		{
			modifiers = 0;
			key = Key.None;

			var parts = PauseHotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			if (parts.Length < 2)
				return false;

			foreach (var part in parts[..^1])
			{
				switch (part.ToLowerInvariant())
				{
					case "ctrl": case "control": modifiers |= Native.PInvoke.Win32.MOD_CONTROL; break;
					case "alt": modifiers |= Native.PInvoke.Win32.MOD_ALT; break;
					case "shift": modifiers |= Native.PInvoke.Win32.MOD_SHIFT; break;
					case "win": modifiers |= Native.PInvoke.Win32.MOD_WIN; break;
					default: return false;
				}
			}

			// Enum.TryParse also accepts raw numbers ("1" would be Key.Cancel); digit keys are spelled D1, NumPad1, ...
			var keyName = parts[^1];
			return !char.IsDigit(keyName[0]) && Enum.TryParse(keyName, ignoreCase: true, out key) && key != Key.None;
		}
	}
}
