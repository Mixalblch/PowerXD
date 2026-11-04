using System;
using System.Threading;
using System.Windows;
using PowerXD.Services;

namespace PowerXD
{
    public partial class App : Application
    {
        private Mutex? _mutex;
        private TrayIconService? _tray;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Load settings and language first so any message is localized.
            try
            {
                SettingsService.Instance.Load();
            }
            catch
            {
                // Settings are optional; fall back to defaults.
            }

            LocalizationService.Instance.SetLanguage(SettingsService.Instance.ResolvedLanguage);

            // Single instance guard.
            _mutex = new Mutex(true, @"Local\PowerXD_SingleInstance_Mutex", out bool isNew);
            if (!isNew)
            {
                MessageBox.Show(
                    LocalizationService.Instance.T("AlreadyRunning"),
                    "PowerXD",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown();
                return;
            }

            var mainWindow = new MainWindow();
            _tray = new TrayIconService(mainWindow);
            mainWindow.Initialize(_tray);

            // First run: show the popup once so the user discovers the app.
            if (SettingsService.Instance.FirstRun)
            {
                SettingsService.Instance.FirstRun = false;
                try { SettingsService.Instance.Save(); } catch { }
                mainWindow.ShowAtTray();
                _tray.ShowBalloon(LocalizationService.Instance.T("AppStarted"));
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _tray?.Dispose();
            try { SettingsService.Instance.Save(); } catch { }
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
