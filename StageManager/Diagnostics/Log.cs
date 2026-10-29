using System;
using System.IO;

namespace StageManager.Diagnostics
{
	/// <summary>
	/// Minimal append-only log in %LOCALAPPDATA%\StageManager. Never throws.
	/// </summary>
	internal static class Log
	{
		private static readonly object _lock = new object();

		public static string Directory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StageManager");

		private static string FilePath => Path.Combine(Directory, "stagemanager.log");

		public static void Info(string message) => Write("INFO ", message);

		public static void Error(string message, Exception? ex = null) => Write("ERROR", ex is null ? message : $"{message}{Environment.NewLine}{ex}");

		private static void Write(string level, string message)
		{
			try
			{
				lock (_lock)
				{
					System.IO.Directory.CreateDirectory(Directory);
					File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}");
				}
			}
			catch
			{
				// logging must never take the app down
			}
		}
	}
}
