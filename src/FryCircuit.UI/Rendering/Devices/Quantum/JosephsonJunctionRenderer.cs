using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Quantum;

/// <summary>
/// Professional IEEE superconducting quantum circuit renderer for Josephson Junctions.
/// </summary>
public class JosephsonJunctionRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var jjDev = device as JosephsonJunctionDevice;

        var strokeBrush = renderContext.IsPlacingGhost
            ? new SolidColorBrush(Color.FromArgb(140, 0, 240, 255))
            : new SolidColorBrush(Color.FromRgb(240, 246, 255));

        var strokePen = new Pen(strokeBrush, 2.5 * z)
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        var boxBrush = new SolidColorBrush(Color.FromArgb(200, 16, 22, 32));
        var boxPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 220, 255)), 1.8 * z);
        var crossPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 215, 0)), 2.2 * z)
        {
            LineCap = PenLineCap.Round
        };

        // Outer box (width 0.8 * u, height 0.8 * u)
        double boxSize = u * 0.75;
        var boxRect = new Rect(center.X - boxSize / 2.0, center.Y - boxSize / 2.0, boxSize, boxSize);

        // Leads from pins (-1 * u to box edge, +1 * u to box edge)
        context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(boxRect.Left, center.Y));
        context.DrawLine(strokePen, new Point(boxRect.Right, center.Y), new Point(center.X + u, center.Y));

        // Draw quantum junction box
        context.DrawRectangle(boxBrush, boxPen, boxRect, 3 * z, 3 * z);

        // Superconducting cross barrier (X inside box)
        double inset = boxSize * 0.22;
        context.DrawLine(crossPen, new Point(boxRect.Left + inset, boxRect.Top + inset), new Point(boxRect.Right - inset, boxRect.Bottom - inset));
        context.DrawLine(crossPen, new Point(boxRect.Left + inset, boxRect.Bottom - inset), new Point(boxRect.Right - inset, boxRect.Top + inset));

        // Terminal solder pads
        context.DrawEllipse(strokeBrush, null, new Point(center.X - u, center.Y), 3.2 * z, 3.2 * z);
        context.DrawEllipse(strokeBrush, null, new Point(center.X + u, center.Y), 3.2 * z, 3.2 * z);
    }
}
