using System;
using System.Drawing;

namespace PowerXD.Services
{
    internal static class PopupPlacement
    {
        // Device-pixel input/output, independent of WPF layout and native APIs.
        internal static Rectangle Fit(Rectangle workArea, Size preferredSize, int margin)
        {
            if (workArea.Width <= 0 || workArea.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(workArea));

            int gapX = Math.Clamp(margin, 0, (workArea.Width - 1) / 2);
            int gapY = Math.Clamp(margin, 0, (workArea.Height - 1) / 2);
            int width = Math.Clamp(preferredSize.Width, 1, workArea.Width - gapX * 2);
            int height = Math.Clamp(preferredSize.Height, 1, workArea.Height - gapY * 2);
            return new Rectangle(workArea.Right - gapX - width,
                workArea.Bottom - gapY - height, width, height);
        }
    }
}
