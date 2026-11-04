using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
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

        public ObservableCollection<PowerScheme> Schemes { get; } = new ObservableCollection<PowerScheme>();

        public MainWindow()
        {
            InitializeComponent();
            ModesList.ItemsSource = Schemes;

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

            PositionNearTray();
            Show();
            Activate();
            AnimateIn();
        }

        public void ToggleVisibility()
        {
            _hideTimer.Stop();
            if (Visibility == Visibility.Visible) Hide();
            else ShowAtTray();
        }

        private void PositionNearTray()
        {
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            if (screen == null) return;
            var area = screen.WorkingArea;

            double scale = GetDpiForSystem() / 96.0;
            if (scale <= 0) scale = 1.0;

            // Force layout so the auto-height is known before anchoring to the bottom.
            Measure(new Size(Width, double.PositiveInfinity));

            var width = ActualWidth > 0 ? ActualWidth : (double.IsNaN(Width) ? 360.0 : Width);
            var height = ActualHeight > 0 ? ActualHeight
                       : (DesiredSize.Height > 0 ? DesiredSize.Height : 220.0);

            Left = (area.Right / scale) - width - 10;
            Top = (area.Bottom / scale) - height - 10;
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

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Hide();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }
    }
}
