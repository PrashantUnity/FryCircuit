using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Capacitor;

public class CapacitorRenderer : IDeviceRenderer
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
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        // Top terminal line & top plate
        context.DrawLine(strokePen, new Point(center.X, center.Y - u), new Point(center.X, center.Y - u * 0.2));
        context.DrawLine(strokePen, new Point(center.X - u * 0.5, center.Y - u * 0.2), new Point(center.X + u * 0.5, center.Y - u * 0.2));

        // Bottom plate & bottom terminal line
        context.DrawLine(strokePen, new Point(center.X - u * 0.5, center.Y + u * 0.2), new Point(center.X + u * 0.5, center.Y + u * 0.2));
        context.DrawLine(strokePen, new Point(center.X, center.Y + u * 0.2), new Point(center.X, center.Y + u));

        // Terminal solder pads
        context.DrawEllipse(strokeBrush, null, new Point(center.X, center.Y - u), 3.2 * z, 3.2 * z);
        context.DrawEllipse(strokeBrush, null, new Point(center.X, center.Y + u), 3.2 * z, 3.2 * z);
    }
}
