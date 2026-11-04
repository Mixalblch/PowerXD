using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

class Program
{
    static int Main(string[] args)
    {
        var outPath = args.Length > 0 ? args[0] : "app.ico";
        var previewPath = args.Length > 1 ? args[1] : null;
        try
        {
            WriteAllBytes(outPath, BuildIcon());

            if (!string.IsNullOrEmpty(previewPath))
            {
                using var preview = DrawIcon(256);
                using var ms = new MemoryStream();
                preview.Save(ms, ImageFormat.Png);
                WriteAllBytes(previewPath, ms.ToArray());
                Console.WriteLine($"Preview written: {Path.GetFullPath(previewPath)}");
            }

            Console.WriteLine($"Icon written: {Path.GetFullPath(outPath)}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    static void WriteAllBytes(string path, byte[] data)
    {
        var full = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(full, data);
    }

    // Classic DIB (BITMAPINFOHEADER + XOR + AND) ICO — supported everywhere.
    static byte[] BuildIcon()
    {
        int[] sizes = { 256, 64, 48, 32, 16 };
        var frames = new List<byte[]>();
        foreach (var size in sizes) frames.Add(BuildDibFrame(size));

        using var ico = new MemoryStream();
        using var bw = new BinaryWriter(ico);

        bw.Write((ushort)0);              // reserved
        bw.Write((ushort)1);              // type: icon
        bw.Write((ushort)sizes.Length);   // count

        int offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            int s = sizes[i];
            byte dim = (byte)(s >= 256 ? 0 : s);

            bw.Write(dim);                  // width
            bw.Write(dim);                  // height
            bw.Write((byte)0);              // palette
            bw.Write((byte)0);              // reserved
            bw.Write((ushort)1);            // color planes
            bw.Write((ushort)32);           // bits per pixel
            bw.Write((uint)frames[i].Length); // data size
            bw.Write((uint)offset);         // data offset
            offset += frames[i].Length;
        }

        foreach (var frame in frames) bw.Write(frame);
        bw.Flush();
        return ico.ToArray();
    }

    static byte[] BuildDibFrame(int size)
    {
        using var bmp = DrawIcon(size);
        var rect = new Rectangle(0, 0, size, size);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = data.Stride;
            int pixelBytes = size * 4;
            int xorSize = size * pixelBytes;
            int andRowSize = ((size + 31) / 32) * 4; // 1bpp rows padded to 32 bits
            int andSize = andRowSize * size;

            var xor = new byte[xorSize];
            // DIB is bottom-up: copy source rows in reverse order.
            for (int y = 0; y < size; y++)
            {
                int srcRow = y;
                int dstRow = size - 1 - y;
                Marshal.Copy(IntPtr.Add(data.Scan0, srcRow * stride), xor, dstRow * pixelBytes, pixelBytes);
            }

            var and = new byte[andSize]; // all zero: opaque; alpha channel drives transparency

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // BITMAPINFOHEADER
            bw.Write((uint)40);                  // biSize
            bw.Write(size);                      // biWidth
            bw.Write(size * 2);                  // biHeight (XOR + AND)
            bw.Write((ushort)1);                 // biPlanes
            bw.Write((ushort)32);                // biBitCount
            bw.Write((uint)0);                   // biCompression (BI_RGB)
            bw.Write((uint)(xorSize + andSize)); // biSizeImage
            bw.Write(0);                         // biXPelsPerMeter
            bw.Write(0);                         // biYPelsPerMeter
            bw.Write((uint)0);                   // biClrUsed
            bw.Write((uint)0);                   // biClrImportant

            bw.Write(xor);
            bw.Write(and);
            bw.Flush();
            return ms.ToArray();
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    static Bitmap DrawIcon(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        float radius = size * 0.22f;
        using var path = RoundedRect(new RectangleF(0, 0, size, size), radius);

        using var gradient = new LinearGradientBrush(
            new PointF(0, 0),
            new PointF(size, size),
            Color.FromArgb(255, 142, 123, 255), // #8E7BFF
            Color.FromArgb(255, 78, 141, 255)); // #4E8DFF
        g.FillPath(gradient, path);

        float cx = size / 2f;
        float cy = size / 2f;
        float r = size * 0.242f;   // arc radius
        float w = size * 0.102f;   // stroke width

        using var pen = new Pen(Color.White, w)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };

        // Vertical line from the center up to the gap.
        g.DrawLine(pen, cx, cy, cx, cy - r);

        // Arc with a 60-degree gap at the top (power symbol).
        var arc = new RectangleF(cx - r, cy - r, r * 2f, r * 2f);
        g.DrawArc(pen, arc, 300f, 300f);

        return bmp;
    }

    static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2f;

        path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
