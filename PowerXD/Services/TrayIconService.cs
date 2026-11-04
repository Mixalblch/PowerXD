using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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

            var tooltip = scheme == null ? "PowerXD" : $"PowerXD — {scheme.Name}";
            _notifyIcon.Text = tooltip.Length <= 63 ? tooltip : tooltip[..60] + "...";
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
            // Use the entire tray glyph for the current mode, so its state is
            // readable at 16px on both light and dark taskbars.
            using var bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var background = new SolidBrush(accent ?? Color.FromArgb(154, 160, 174));
                g.FillEllipse(background, 1, 1, 30, 30);

                using var pen = new Pen(Color.White, 2.5f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                g.DrawArc(pen, 7, 7, 18, 18, -45, 270);
                g.DrawLine(pen, 16, 5, 16, 15);
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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.MouseClick -= OnMouseClick;
            _notifyIcon.Icon = null;
            _notifyIcon.Dispose();
            _currentIcon?.Dispose();
            _currentIcon = null;
        }
    }
}
