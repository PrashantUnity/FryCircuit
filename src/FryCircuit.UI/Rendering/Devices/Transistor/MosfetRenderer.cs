using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Transistor;

public class MosfetRenderer : IDeviceRenderer
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

        bool isNChannel = (device as MosfetDevice)?.IsNChannel ?? !device.Type.Equals("pmos", StringComparison.OrdinalIgnoreCase);

        // Gate line
        double gX = center.X - u * 0.28;
        if (!isNChannel)
        {
            // PMOS: Gate inversion bubble
            double bubbleRadius = 3.5 * z;
            double bubbleCenterX = gX - bubbleRadius;
            context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(bubbleCenterX - bubbleRadius, center.Y));
            context.DrawEllipse(new SolidColorBrush(Color.FromRgb(15, 20, 30)), strokePen, new Point(bubbleCenterX, center.Y), bubbleRadius, bubbleRadius);
        }
        else
        {
            context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(gX, center.Y));
        }

        // Gate dielectric plate
        context.DrawLine(new Pen(strokeBrush, 3.5 * z) { LineCap = PenLineCap.Round }, new Point(gX, center.Y - u * 0.45), new Point(gX, center.Y + u * 0.45));

        // Channel segmented lines
        double chX = center.X - u * 0.12;
        context.DrawLine(strokePen, new Point(chX, center.Y - u * 0.45), new Point(chX, center.Y - u * 0.15));
        context.DrawLine(strokePen, new Point(chX, center.Y - u * 0.08), new Point(chX, center.Y + u * 0.08));
        context.DrawLine(strokePen, new Point(chX, center.Y + u * 0.15), new Point(chX, center.Y + u * 0.45));

        // Drain lead (Pin 1 at (1, -1)): goes from top channel segment horizontally to (center.X + u), then vertically straight up to terminal
        context.DrawLine(strokePen, new Point(chX, center.Y - u * 0.3), new Point(center.X + u, center.Y - u * 0.3));
        context.DrawLine(strokePen, new Point(center.X + u, center.Y - u * 0.3), new Point(center.X + u, center.Y - u));

        // Source lead (Pin 2 at (1, 1)): goes from bottom channel segment horizontally to (center.X + u), then vertically straight down to terminal
        context.DrawLine(strokePen, new Point(chX, center.Y + u * 0.3), new Point(center.X + u, center.Y + u * 0.3));
        context.DrawLine(strokePen, new Point(center.X + u, center.Y + u * 0.3), new Point(center.X + u, center.Y + u));

        // Substrate / Bulk connection: from middle channel segment, ties into source lead
        double subConnX = center.X + u * 0.45;
        context.DrawLine(strokePen, new Point(chX, center.Y), new Point(subConnX, center.Y));
        context.DrawLine(strokePen, new Point(subConnX, center.Y), new Point(subConnX, center.Y + u * 0.3));

        // Substrate arrow on middle lead
        var arrowPen = new Pen(strokeBrush, 2.4 * z) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        double arrowTipX = isNChannel ? (chX + u * 0.1) : (subConnX - u * 0.1);
        double arrowBaseX = isNChannel ? (arrowTipX + u * 0.14) : (arrowTipX - u * 0.14);
        context.DrawLine(arrowPen, new Point(arrowBaseX, center.Y - u * 0.1), new Point(arrowTipX, center.Y));
        context.DrawLine(arrowPen, new Point(arrowBaseX, center.Y + u * 0.1), new Point(arrowTipX, center.Y));

        // Terminal solder pads
        context.DrawEllipse(strokeBrush, null, new Point(center.X - u, center.Y), 3.2 * z, 3.2 * z);
        context.DrawEllipse(strokeBrush, null, new Point(center.X + u, center.Y - u), 3.2 * z, 3.2 * z);
        context.DrawEllipse(strokeBrush, null, new Point(center.X + u, center.Y + u), 3.2 * z, 3.2 * z);
    }
}
