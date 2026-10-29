using System;
using System.IO;

namespace StageManager.Diagnostics
{
	/// <summary>
	/// Remembers the desktop-icon visibility from before Stage Manager touched it, so it can be restored
	/// even after the previous session was killed while icons were hidden.
	/// The file only exists while a session is running; finding it at startup means the last exit was unclean.
	/// </summary>
	internal static class RecoveryJournal
	{
		private static string FilePath => Path.Combine(Log.Directory, "session.txt");

		/// <summary>
		/// Starts a session and returns the icon visibility to restore on exit.
		/// </summary>
		public static bool Begin(bool currentIconsVisible)
		{
			try
			{
				if (File.Exists(FilePath) && bool.TryParse(File.ReadAllText(FilePath).Trim(), out var original))
				{
					Log.Info($"Previous session did not exit cleanly; original desktop icon visibility was {original}");
					return original;
				}

				Directory.CreateDirectory(Log.Directory);
				File.WriteAllText(FilePath, currentIconsVisible.ToString());
			}
			catch (Exception ex)
			{
				Log.Error("Could not write recovery journal", ex);
			}

			return currentIconsVisible;
		}

		public static void End()
		{
			try
			{
				File.Delete(FilePath);
			}
			catch (Exception ex)
			{
				Log.Error("Could not delete recovery journal", ex);
			}
		}
	}
}
