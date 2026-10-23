using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Quantum;

/// <summary>
/// Professional IEEE Vector CAD renderer for DC Superconducting Quantum Interference Device (SQUID).
/// Renders superconducting double loop with two Josephson junctions and magnetic flux bias symbol.
/// </summary>
public class SquidRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double size, bool isSelected, DeviceRenderContext renderContext)
    {
        var squid = device as SquidDevice;
        var leadColor = renderContext.IsDark ? Color.FromRgb(0, 240, 255) : Color.FromRgb(2, 132, 199);
        var pen = new Pen(new SolidColorBrush(leadColor), 2.2 * renderContext.Zoom);
        var junctionBrush = renderContext.GetComponentFillBrush() ?? new SolidColorBrush(Color.FromRgb(16, 26, 42));
        var junctionPen = renderContext.GetStrokePen(1.5, Color.FromRgb(255, 200, 0), Color.FromRgb(217, 119, 6));
        var crossPen = renderContext.GetStrokePen(1.8, Color.FromRgb(255, 220, 50), Color.FromRgb(217, 119, 6));

        double halfH = size;
        double loopW = size * 0.45;
        double jBoxSize = size * 0.22;

        // Top and bottom lead lines
        context.DrawLine(pen, new Point(center.X, center.Y - halfH), new Point(center.X, center.Y - size * 0.35));
        context.DrawLine(pen, new Point(center.X, center.Y + halfH), new Point(center.X, center.Y + size * 0.35));

        // Superconducting Split Loop
        // Left Branch
        context.DrawLine(pen, new Point(center.X, center.Y - size * 0.35), new Point(center.X - loopW, center.Y - size * 0.35));
        context.DrawLine(pen, new Point(center.X - loopW, center.Y - size * 0.35), new Point(center.X - loopW, center.Y - jBoxSize));
        context.DrawLine(pen, new Point(center.X - loopW, center.Y + jBoxSize), new Point(center.X - loopW, center.Y + size * 0.35));
        context.DrawLine(pen, new Point(center.X - loopW, center.Y + size * 0.35), new Point(center.X, center.Y + size * 0.35));

        // Right Branch
        context.DrawLine(pen, new Point(center.X, center.Y - size * 0.35), new Point(center.X + loopW, center.Y - size * 0.35));
        context.DrawLine(pen, new Point(center.X + loopW, center.Y - size * 0.35), new Point(center.X + loopW, center.Y - jBoxSize));
        context.DrawLine(pen, new Point(center.X + loopW, center.Y + jBoxSize), new Point(center.X + loopW, center.Y + size * 0.35));
        context.DrawLine(pen, new Point(center.X + loopW, center.Y + size * 0.35), new Point(center.X, center.Y + size * 0.35));

        // Left Josephson Junction
        DrawJosephsonBox(context, new Point(center.X - loopW, center.Y), jBoxSize, junctionBrush, junctionPen, crossPen);

        // Right Josephson Junction
        DrawJosephsonBox(context, new Point(center.X + loopW, center.Y), jBoxSize, junctionBrush, junctionPen, crossPen);

        // Central Magnetic Flux Symbol (Circle with dot / cross)
        var fluxColor = renderContext.IsDark ? Color.FromArgb(180, 0, 240, 255) : Color.FromArgb(180, 2, 132, 199);
        var fluxBrush = new SolidColorBrush(fluxColor);
        context.DrawEllipse(null, new Pen(fluxBrush, 1.2 * renderContext.Zoom), center, size * 0.12, size * 0.12);
        context.DrawEllipse(fluxBrush, null, center, 2.0 * renderContext.Zoom, 2.0 * renderContext.Zoom);

        // Terminal solder dots
        var dotBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(dotBrush, null, new Point(center.X, center.Y - halfH), 2.5 * renderContext.Zoom, 2.5 * renderContext.Zoom);
        context.DrawEllipse(dotBrush, null, new Point(center.X, center.Y + halfH), 2.5 * renderContext.Zoom, 2.5 * renderContext.Zoom);
    }

    private void DrawJosephsonBox(DrawingContext context, Point pt, double s, IBrush fill, Pen borderPen, Pen crossPen)
    {
        var rect = new Rect(pt.X - s / 2.0, pt.Y - s / 2.0, s, s);
        context.DrawRectangle(fill, borderPen, rect, 2, 2);
        context.DrawLine(crossPen, new Point(rect.Left + 2, rect.Top + 2), new Point(rect.Right - 2, rect.Bottom - 2));
        context.DrawLine(crossPen, new Point(rect.Left + 2, rect.Bottom - 2), new Point(rect.Right - 2, rect.Top + 2));
    }
}
