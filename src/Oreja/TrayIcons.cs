using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Oreja;

/// <summary>Zeichnet die vier Tray-Icons in der gewünschten Größe (keine Bilddateien nötig).</summary>
public sealed class TrayIcons : IDisposable
{
    private static readonly Color Grey = Color.FromArgb(0x8A, 0x8A, 0x8A);
    private static readonly Color Red = Color.FromArgb(0xE5, 0x39, 0x35);
    private static readonly Color Yellow = Color.FromArgb(0xFF, 0xC1, 0x07);
    private static readonly Color Outline = Color.FromArgb(0x20, 0x20, 0x20);

    private readonly Dictionary<TrayIconKind, Icon> _icons = [];
    private readonly List<IntPtr> _handles = [];

    public TrayIcons(int size)
    {
        foreach (var kind in Enum.GetValues<TrayIconKind>()) _icons[kind] = Draw(kind, size);
    }

    public Icon this[TrayIconKind kind] => _icons[kind];

    private Icon Draw(TrayIconKind kind, int s)
    {
        using var bitmap = new Bitmap(s, s);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float pad = s / 8f;
            var circle = new RectangleF(pad, pad, s - 2 * pad, s - 2 * pad);

            if (kind is TrayIconKind.Idle or TrayIconKind.Error)
            {
                using var ring = new Pen(Grey, Math.Max(2f, s / 8f));
                g.DrawEllipse(ring, circle);
            }
            else
            {
                using var fill = new SolidBrush(Red);
                g.FillEllipse(fill, circle);
            }

            using var yellow = new SolidBrush(Yellow);
            using var outline = new Pen(Outline, 1f);
            if (kind == TrayIconKind.RecordingDegraded)
            {
                float d = s * 0.45f;
                var dot = new RectangleF(s - d, s - d, d - 1, d - 1);
                g.FillEllipse(yellow, dot);
                g.DrawEllipse(outline, dot);
            }
            else if (kind == TrayIconKind.Error)
            {
                float h = s * 0.55f;
                PointF[] triangle = [new(s - h / 2 - 0.5f, s - h), new(s - 0.5f, s - 0.5f), new(s - h - 0.5f, s - 0.5f)];
                g.FillPolygon(yellow, triangle);
                g.DrawPolygon(outline, triangle);
            }
        }

        IntPtr handle = bitmap.GetHicon();
        _handles.Add(handle);
        return Icon.FromHandle(handle);
    }

    public void Dispose()
    {
        foreach (var icon in _icons.Values) icon.Dispose();
        foreach (var handle in _handles) DestroyIcon(handle);
        _icons.Clear();
        _handles.Clear();
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
