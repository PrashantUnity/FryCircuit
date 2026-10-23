using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Transistor;

public class BjtRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var strokeBrush = renderContext.GetStrokeBrush();
        var strokePen = renderContext.GetStrokePen(2.8);

        bool isNpn = (device as BjtDevice)?.IsNpn ?? !device.Type.Equals("pnp", StringComparison.OrdinalIgnoreCase);

        // Transistor circle frame
        double radius = u * 0.75;
        var fillBrush = renderContext.GetComponentFillBrush() ?? new SolidColorBrush(Color.FromRgb(20, 28, 40));
        var framePen = renderContext.GetStrokePen(1.5, Color.FromArgb(60, 160, 200, 240), Color.FromArgb(80, 71, 85, 105));
        context.DrawEllipse(fillBrush, framePen, center, radius, radius);

        // Base vertical bar
        double bX = center.X - u * 0.25;
        context.DrawLine(new Pen(strokeBrush, 4.0 * z) { LineCap = PenLineCap.Round }, new Point(bX, center.Y - u * 0.45), new Point(bX, center.Y + u * 0.45));

        // Base lead (Pin 0 at (-1, 0))
        context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(bX, center.Y));

        // Collector lead (Pin 1 at (1, -1))
        context.DrawLine(strokePen, new Point(bX, center.Y - u * 0.25), new Point(center.X + u * 0.4, center.Y - u * 0.6));
        context.DrawLine(strokePen, new Point(center.X + u * 0.4, center.Y - u * 0.6), new Point(center.X + u, center.Y - u));

        // Emitter lead (Pin 2 at (1, 1))
        Point emStart = new Point(bX, center.Y + u * 0.25);
        Point emMid = new Point(center.X + u * 0.4, center.Y + u * 0.6);
        context.DrawLine(strokePen, emStart, emMid);
        context.DrawLine(strokePen, emMid, new Point(center.X + u, center.Y + u));

        // Arrow on emitter
        var arrowPen = new Pen(strokeBrush, 2.5 * z) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        if (isNpn)
        {
            // NPN: Arrow points away from base
            context.DrawLine(arrowPen, new Point(emMid.X - u * 0.15, emMid.Y - u * 0.05), emMid);
            context.DrawLine(arrowPen, new Point(emMid.X - u * 0.05, emMid.Y - u * 0.15), emMid);
        }
        else
        {
            // PNP: Arrow points toward base
            context.DrawLine(arrowPen, new Point(emStart.X + u * 0.15, emStart.Y + u * 0.05), emStart);
            context.DrawLine(arrowPen, new Point(emStart.X + u * 0.05, emStart.Y + u * 0.15), emStart);
        }

        // Terminal solder pads
        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y), 3.2 * z, 3.2 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y - u), 3.2 * z, 3.2 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y + u), 3.2 * z, 3.2 * z);
    }
}
