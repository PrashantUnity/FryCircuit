using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Ground;

public class GroundRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var strokeBrush = renderContext.IsPlacingGhost
            ? new SolidColorBrush(Color.FromArgb(140, 0, 240, 255))
            : new SolidColorBrush(Color.FromRgb(240, 246, 255));

        var strokePen = new Pen(strokeBrush, 2.8 * z)
        {
            LineCap = PenLineCap.Round
        };

        // Stem from top pin terminal down to ground bars
        context.DrawLine(strokePen, new Point(center.X, center.Y), new Point(center.X, center.Y + u * 0.35));

        // 3 descending horizontal ground lines
        context.DrawLine(strokePen, new Point(center.X - u * 0.30, center.Y + u * 0.35), new Point(center.X + u * 0.30, center.Y + u * 0.35));
        context.DrawLine(strokePen, new Point(center.X - u * 0.19, center.Y + u * 0.52), new Point(center.X + u * 0.19, center.Y + u * 0.52));
        context.DrawLine(strokePen, new Point(center.X - u * 0.09, center.Y + u * 0.68), new Point(center.X + u * 0.09, center.Y + u * 0.68));

        // Terminal solder pad
        context.DrawEllipse(strokeBrush, null, new Point(center.X, center.Y), 3.0 * z, 3.0 * z);
    }
}
