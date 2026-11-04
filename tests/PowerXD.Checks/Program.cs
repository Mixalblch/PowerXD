using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using PowerXD;
using PowerXD.Models;
using PowerXD.Services;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;

internal static class Program
{
    private static int _checks;

    [STAThread]
    private static int Main()
    {
        System.Windows.Application? application = null;
        try
        {
            CheckPlacementMath();
            CheckPalette();
            application = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            application.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/PowerXD;component/Themes/Styles.xaml")
            });
            CheckPopupLayout();
            Console.WriteLine($"PASS: {_checks} regression checks");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
        finally
        {
            application?.Shutdown();
        }
    }

    private static void CheckPlacementMath()
    {
        var areas = new[]
        {
            new DrawingRectangle(0, 0, 1920, 1040),
            new DrawingRectangle(-1920, 0, 1920, 1040),
            new DrawingRectangle(0, -1080, 1920, 1040),
            new DrawingRectangle(2560, -200, 3840, 2080),
            new DrawingRectangle(48, 0, 1872, 1080), // left taskbar
            new DrawingRectangle(0, 48, 1920, 1032), // top taskbar
        };
        foreach (var area in areas)
        {
            foreach (double scale in new[] { 1.0, 1.25, 1.5, 2.0 })
            {
                int margin = (int)Math.Round(10 * scale);
                var size = new DrawingSize((int)(360 * scale), (int)(500 * scale));
                var bounds = PopupPlacement.Fit(area, size, margin);
                Expect(area.Contains(bounds), "Popup stays inside its monitor work area");
                Expect(bounds.Right == area.Right - margin, "Right edge stays anchored");
                Expect(bounds.Bottom == area.Bottom - margin, "Bottom edge stays anchored");
            }
        }
        var tiny = new DrawingRectangle(-200, -100, 90, 60);
        Expect(tiny.Contains(PopupPlacement.Fit(tiny, new DrawingSize(720, 900), 20)),
            "Oversized popup is constrained to the work area");
        var pixel = new DrawingRectangle(-1, -1, 1, 1);
        Expect(PopupPlacement.Fit(pixel, DrawingSize.Empty, 100) == pixel,
            "Very small work areas still produce valid bounds");
    }

    private static void CheckPalette()
    {
        Expect(new PowerScheme { Guid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" }.ColorHex == "#FF5A5F",
            "High performance is red");
        Expect(new PowerScheme { Guid = "381B4222-F694-41F0-9685-FF5BB260DF2E" }.ColorHex == "#68B7FF",
            "Balanced is light blue, including uppercase GUIDs");
        Expect(new PowerScheme { Guid = "a1841308-3541-4fab-bc81-f71556f20b4a" }.ColorHex == "#34C759",
            "Power saver is green");
        Expect(new PowerScheme { Guid = "custom" }.ColorHex == "#9AA0AE",
            "Unknown schemes retain a neutral fallback");
    }

    private static void CheckPopupLayout()
    {
        var window = new MainWindow();
        try
        {
            window.Schemes.Add(new PowerScheme
            {
                Guid = "381b4222-f694-41f0-9685-ff5bb260df2e",
                Name = "Сбалансированный / Balanced",
                IsActive = true
            });
            window.ShowAtTray();
            FlushLayout();
            CheckWindowBounds(window);
            double initialHeight = window.ActualHeight;

            // Reproduce asynchronous discovery increasing SizeToContent.Height.
            for (int i = 0; i < 10; i++)
                window.Schemes.Add(new PowerScheme
                {
                    Name = "Дополнительная схема электропитания с длинным названием " + i
                });
            FlushLayout();
            Expect(window.ActualHeight > initialHeight, "Popup grows after scheme discovery");
            CheckWindowBounds(window);
            Capture(window);

            window.Schemes.Clear();
            FlushLayout();
            CheckWindowBounds(window);
            window.Hide();
            window.ShowAtTray();
            FlushLayout();
            CheckWindowBounds(window);
        }
        finally
        {
            window.Close();
        }
    }

    private static void FlushLayout()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void CheckWindowBounds(MainWindow window)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        Expect(GetWindowRect(hwnd, out var rect), "Native popup rectangle is available");
        var bounds = DrawingRectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        var area = System.Windows.Forms.Screen.FromHandle(hwnd).WorkingArea;
        Expect(area.Contains(bounds), "Real WPF popup stays inside the taskbar work area");
    }

    private static void Capture(MainWindow window)
    {
        Directory.CreateDirectory("artifacts");
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),
            (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create("artifacts/popup-regression.png");
        encoder.Save(stream);
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out WindowRect rect);
}
