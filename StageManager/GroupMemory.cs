using StageManager.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace StageManager
{
	/// <summary>
	/// App sets the user built by moving windows between scenes: process name → scene key.
	/// When every window of an app ends up in another app's scene, later windows of that app open there too,
	/// also after a restart. Persisted to %LOCALAPPDATA%\StageManager\groups.json (app state, not a setting).
	/// </summary>
	internal sealed class GroupMemory
	{
		private readonly Dictionary<string, string> _groups = new(StringComparer.OrdinalIgnoreCase);
		private readonly string? _filePath;

		/// <summary>In-memory only (tests).</summary>
		public GroupMemory()
		{
		}

		private GroupMemory(string filePath)
		{
			_filePath = filePath;
		}

		public static string DefaultFilePath => Path.Combine(Log.Directory, "groups.json");

		public static GroupMemory Load(string filePath)
		{
			var memory = new GroupMemory(filePath);

			try
			{
				if (File.Exists(filePath) && JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(filePath)) is { } saved)
				{
					foreach (var (process, key) in saved)
					{
						if (!string.IsNullOrWhiteSpace(process) && !string.IsNullOrWhiteSpace(key))
							memory._groups[process] = key;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error($"Could not read {filePath}; starting without learned app sets", ex);
			}

			return memory;
		}

		public string? Get(string processName) => _groups.TryGetValue(processName, out var key) ? key : null;

		public void Remember(string processName, string key)
		{
			if (Get(processName) == key)
				return;

			_groups[processName] = key;
			Save();
		}

		public void Forget(string processName)
		{
			if (_groups.Remove(processName))
				Save();
		}

		public void Clear()
		{
			_groups.Clear();
			Save();
		}

		private void Save()
		{
			if (_filePath is null)
				return;

			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
				File.WriteAllText(_filePath, JsonSerializer.Serialize(_groups, new JsonSerializerOptions { WriteIndented = true }));
			}
			catch (Exception ex)
			{
				Log.Error($"Could not write {_filePath}", ex);
			}
		}
	}
}
