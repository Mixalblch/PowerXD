using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PowerXD.Models;
using PowerXD.Services;

namespace PowerXD
{
    public partial class MainWindow : Window
    {
        private readonly System.Windows.Threading.DispatcherTimer _hideTimer;
        private DateTime _shownAt = DateTime.MinValue;
        private TrayIconService? _tray;
        private bool _loading = true;
        private bool _positionQueued;
        private bool _positioning;
        private System.Windows.Forms.Screen? _popupScreen;

        public ObservableCollection<PowerScheme> Schemes { get; } = new ObservableCollection<PowerScheme>();

        public MainWindow()
        {
            InitializeComponent();
            ModesList.ItemsSource = Schemes;
            SizeChanged += (_, _) => QueuePositionNearTray();
            ContentRendered += (_, _) => QueuePositionNearTray();

            // Hide the popup slightly after it loses focus. The delay lets a
            // tray-icon click (which also deactivates the window) cancel it and
            // toggle the window instead.
            _hideTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); Hide(); };
        }

        public void Initialize(TrayIconService tray)
        {
            _tray = tray;

            // Populate controls (events are suppressed by _loading).
            AutoStartToggle.IsChecked = AutoStartService.IsEnabled();

            var lang = LocalizationService.Instance.Current;
            LangRu.IsChecked = lang == "ru";
            LangEn.IsChecked = lang != "ru";

            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            ApplyLocalization();

            _ = LoadSchemesAsync();
        }

        private async Task LoadSchemesAsync()
        {
            try
            {
                var schemes = await PowerService.GetSchemesAsync();

                Schemes.Clear();
                foreach (var scheme in schemes) Schemes.Add(scheme);

                PowerScheme? active = null;
                foreach (var scheme in Schemes)
                {
                    if (scheme.IsActive) { active = scheme; break; }
                }

                _tray?.SetActiveScheme(active);
            }
            catch
            {
                // Keep controls functional even if enumeration fails.
            }
            finally
            {
                _loading = false;
            }
        }

        private async void Mode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: PowerScheme scheme }) return;

            if (scheme.IsActive)
            {
                Hide();
                return;
            }

            var ok = await PowerService.SetActiveAsync(scheme.Guid);
            if (ok)
            {
                foreach (var s in Schemes) s.IsActive = s == scheme;
                _tray?.SetActiveScheme(scheme);
                Hide();
            }
            else
            {
                _tray?.ShowBalloon(LocalizationService.Instance.T("SwitchFailed"));
            }
        }

        private void AutoStart_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading || AutoStartToggle.IsChecked == null) return;
            AutoStartService.SetEnabled(AutoStartToggle.IsChecked.Value);
        }

        private void Lang_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            var lang = LangRu.IsChecked == true ? "ru" : "en";
            SettingsService.Instance.Language = lang;
            LocalizationService.Instance.SetLanguage(lang);
            try { SettingsService.Instance.Save(); } catch { }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void OnLanguageChanged(object? sender, EventArgs e) => ApplyLocalization();

        private void ApplyLocalization()
        {
            var loc = LocalizationService.Instance;
            SubtitleText.Text = loc.T("SelectMode");
            RunAtStartupText.Text = loc.T("RunAtStartup");
            LanguageText.Text = loc.T("Language");
            ExitText.Text = loc.T("Exit");
        }

        public void ShowAtTray()
        {
            _shownAt = DateTime.UtcNow;
            _hideTimer.Stop();

            var card = (FrameworkElement)FindName("Card");
            card.Opacity = 0;
            card.RenderTransformOrigin = new Point(0.5, 0.5);
            card.RenderTransform = new ScaleTransform(0.96, 0.96);

            _popupScreen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            Show();
            UpdateLayout();
            PositionNearTray();
            Activate();
            AnimateIn();
        }

        public void ToggleVisibility()
        {
            _hideTimer.Stop();
            if (Visibility == Visibility.Visible) Hide();
            else ShowAtTray();
        }

        private void QueuePositionNearTray()
        {
            if (!IsVisible || _positionQueued) return;
            _positionQueued = true;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                _positionQueued = false;
                if (IsVisible) PositionNearTray();
            }));
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            QueuePositionNearTray();
        }

        private void PositionNearTray()
        {
            if (!IsVisible || _positioning) return;
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            _positioning = true;
            try
            {
                // Keep the chosen monitor stable while async scheme loading changes
                // the popup height. Refresh Screen so taskbar/display changes are read.
                var screen = System.Windows.Forms.Screen.FromHandle(hwnd);
                foreach (var candidate in System.Windows.Forms.Screen.AllScreens)
                {
                    if (candidate.DeviceName == _popupScreen?.DeviceName)
                    {
                        screen = candidate;
                        break;
                    }
                }

                var area = screen.WorkingArea;
                var dpi = VisualTreeHelper.GetDpi(this);
                const int marginDip = 10;
                MaxWidth = Math.Max(1, area.Width / dpi.DpiScaleX - marginDip * 2);
                MaxHeight = Math.Max(1, area.Height / dpi.DpiScaleY - marginDip * 2);
                UpdateLayout();

                // Screen.WorkingArea and GetWindowRect are both physical pixels
                // under the PerMonitorV2 manifest. Do not divide desktop origins
                // by a global DPI: that breaks mixed-DPI and negative-origin monitors.
                if (!GetWindowRect(hwnd, out var rect)) return;
                int margin = Math.Max(0, (int)Math.Round(marginDip * dpi.DpiScaleY));
                var bounds = PopupPlacement.Fit(area,
                    new System.Drawing.Size(rect.Right - rect.Left, rect.Bottom - rect.Top), margin);
                SetWindowPos(hwnd, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height,
                    0x0004 | 0x0010); // SWP_NOZORDER | SWP_NOACTIVATE
            }
            finally
            {
                _positioning = false;
            }
        }

        private void AnimateIn()
        {
            var card = (FrameworkElement)FindName("Card");

            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var scale = new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            card.BeginAnimation(OpacityProperty, fade);
            if (card.RenderTransform is ScaleTransform st)
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
            }
        }

        private void Window_Deactivated(object? sender, EventArgs e)
        {
            // Ignore a deactivation that happens right after showing (focus race).
            if ((DateTime.UtcNow - _shownAt).TotalMilliseconds < 200) return;
            _hideTimer.Stop();
            _hideTimer.Start();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowRect
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hwnd, out WindowRect rect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter,
            int x, int y, int width, int height, uint flags);

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Hide();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }
    }
}
