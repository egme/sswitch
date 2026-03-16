using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace sswitch;

internal static class IconGenerator
{
    // Active: Windows 11 accent blue fill, white digit
    private static readonly Color ActiveFill = Color.FromArgb(0, 120, 212);
    private static readonly Color ActiveText = Color.White;

    // Inactive: subtle outline + muted digit, readable on both dark and light taskbars
    private static readonly Color InactiveBorder = Color.FromArgb(160, 160, 160);
    private static readonly Color InactiveText   = Color.FromArgb(200, 200, 200);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static Icon Generate(int number, bool isActive, int size)
    {
        using var bmp = Draw(number, isActive, size);
        IntPtr hIcon  = bmp.GetHicon();
        var icon      = (Icon)Icon.FromHandle(hIcon).Clone();
        DestroyIcon(hIcon);
        return icon;
    }

    private static Bitmap Draw(int number, bool isActive, int size)
    {
        var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);

        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.PixelOffsetMode   = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        // Rounded rectangle — 1px margin so antialiased edges aren't clipped
        int margin = 1;
        int radius = Math.Max(2, size / 5);   // ~20% corner radius
        var rect   = new Rectangle(margin, margin, size - margin * 2 - 1, size - margin * 2 - 1);

        using var path = RoundedRect(rect, radius);

        if (isActive)
        {
            using var fill = new SolidBrush(ActiveFill);
            g.FillPath(fill, path);
        }
        else
        {
            using var pen = new Pen(InactiveBorder, 1f);
            g.DrawPath(pen, path);
        }

        // Digit — StringFormat centers perfectly for all glyphs including narrow "1"
        float fontSize = size * 0.56f;
        using var font  = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(isActive ? ActiveText : InactiveText);
        using var sf    = new StringFormat
        {
            Alignment     = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };

        string text = number <= 9 ? number.ToString() : "+";
        g.DrawString(text, font, brush, new RectangleF(0, 0, size, size), sf);

        return bmp;
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d    = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X,         r.Y,          d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y,          d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);
        path.AddArc(r.X,         r.Bottom - d, d, d,  90, 90);
        path.CloseFigure();
        return path;
    }
}
