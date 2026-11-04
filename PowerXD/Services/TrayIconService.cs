using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using PowerXD.Models;

namespace PowerXD.Services
{
    public sealed class TrayIconService : IDisposable
    {
        private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
        private readonly MainWindow _window;
        private Icon? _currentIcon;

        public TrayIconService(MainWindow window)
        {
            _window = window;
            _currentIcon = BuildIcon(null);

            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = _currentIcon,
                Visible = true,
                Text = "PowerXD",
            };

            _notifyIcon.MouseClick += OnMouseClick;
        }

        public void SetActiveScheme(PowerScheme? scheme)
        {
            var newIcon = BuildIcon(scheme?.AccentColor);
            var oldIcon = _currentIcon;
            _currentIcon = newIcon;
            _notifyIcon.Icon = newIcon;
            oldIcon?.Dispose();

            _notifyIcon.Text = scheme == null ? "PowerXD" : $"PowerXD — {scheme.Name}";
        }

        public void ShowBalloon(string text)
        {
            _notifyIcon.BalloonTipTitle = "PowerXD";
            _notifyIcon.BalloonTipText = text;
            _notifyIcon.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
            _notifyIcon.ShowBalloonTip(4000);
        }

        private void OnMouseClick(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button != System.Windows.Forms.MouseButtons.Left) return;
            _window.Dispatcher.Invoke(_window.ToggleVisibility);
        }

        private static Icon BuildIcon(Color? accent)
        {
            using var baseIcon = LoadBaseIcon();
            using var bmp = baseIcon.ToBitmap();

            if (accent.HasValue)
            {
                using var g = Graphics.FromImage(bmp);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int d = Math.Max(8, bmp.Width / 4);
                int x = bmp.Width - d - 2;
                int y = bmp.Height - d - 2;

                using var brush = new SolidBrush(accent.Value);
                g.FillEllipse(brush, x, y, d, d);
                using var pen = new Pen(Color.White, Math.Max(1f, d / 8f));
                g.DrawEllipse(pen, x, y, d, d);
            }

            IntPtr hIcon = bmp.GetHicon();
            try
            {
                using var icon = Icon.FromHandle(hIcon);
                return (Icon)icon.Clone();
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }

        private static Icon LoadBaseIcon()
        {
            var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("PowerXD.Assets.app.ico");
            if (stream == null) return SystemIcons.Application;

            using (stream)
            {
                return new Icon(stream, new Size(64, 64));
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.MouseClick -= OnMouseClick;
            _notifyIcon.Dispose(); // also disposes the current icon
        }
    }
}
