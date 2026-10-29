using AsyncAwaitBestPractices;
using StageManager.Diagnostics;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace StageManager
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		private Mutex? _singleInstanceMutex;

		protected override void OnStartup(StartupEventArgs e)
		{
			// two instances would fight over the same windows
			_singleInstanceMutex = new Mutex(initiallyOwned: true, @"Local\StageManager-7d6c1f0e", out var createdNew);
			if (!createdNew)
			{
				MessageBox.Show("Stage Manager is already running. Use its tray icon to pause or quit it.", "Stage Manager", MessageBoxButton.OK, MessageBoxImage.Information);
				Shutdown();
				return;
			}

			DispatcherUnhandledException += OnDispatcherUnhandledException;
			AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
			TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
			SafeFireAndForgetExtensions.SetDefaultExceptionHandling(ex => Log.Error("Background task failed", ex)); // otherwise swallowed silently

			Log.Info("Started");
			Settings.Load();
			base.OnStartup(e);

			// created here rather than via StartupUri so a second instance never builds a window that grabs the desktop
			MainWindow = new MainWindow();
			MainWindow.Show();
		}

		protected override void OnExit(ExitEventArgs e)
		{
			RestoreDesktop();
			Log.Info("Exited");
			base.OnExit(e);
		}

		private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			Log.Error("Unhandled exception on the UI thread; restoring windows and quitting", e.Exception);
			RestoreDesktop();
			e.Handled = true;
			Shutdown(1);
		}

		private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			// the process is terminating; all we can do is put the desktop back
			Log.Error("Unhandled exception; restoring windows before the process terminates", e.ExceptionObject as Exception);
			RestoreDesktop();
		}

		private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
		{
			Log.Error("Unobserved task exception", e.Exception);
			e.SetObserved();
		}

		private static void RestoreDesktop()
		{
			try
			{
				SceneManager.Active?.Stop();
			}
			catch (Exception ex)
			{
				Log.Error("Restoring the desktop failed", ex);
			}
		}
	}
}
