using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.CurrentSource;

public class CurrentSourceRenderer : IDeviceRenderer
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

        var fillBrush = new SolidColorBrush(Color.FromRgb(20, 28, 40));

        // Leads from pins to circle
        double radius = u * 0.55;
        context.DrawLine(strokePen, new Point(center.X, center.Y - u), new Point(center.X, center.Y - radius));
        context.DrawLine(strokePen, new Point(center.X, center.Y + radius), new Point(center.X, center.Y + u));

        // Source circle
        context.DrawEllipse(fillBrush, strokePen, center, radius, radius);

        // Arrow indicating current direction (downward from + to -)
        var arrowBrush = new SolidColorBrush(Color.FromRgb(0, 230, 118));
        var arrowPen = new Pen(arrowBrush, 2.4 * z) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };

        context.DrawLine(arrowPen, new Point(center.X, center.Y - u * 0.28), new Point(center.X, center.Y + u * 0.22));
        context.DrawLine(arrowPen, new Point(center.X - u * 0.15, center.Y + u * 0.08), new Point(center.X, center.Y + u * 0.25));
        context.DrawLine(arrowPen, new Point(center.X + u * 0.15, center.Y + u * 0.08), new Point(center.X, center.Y + u * 0.25));

        // Terminal solder pads
        context.DrawEllipse(strokeBrush, null, new Point(center.X, center.Y - u), 3.2 * z, 3.2 * z);
        context.DrawEllipse(strokeBrush, null, new Point(center.X, center.Y + u), 3.2 * z, 3.2 * z);
    }
}
