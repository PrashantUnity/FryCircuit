using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Quantum;

/// <summary>
/// IEEE standard schematic symbol for Tunnel Diode (Esaki Diode).
/// </summary>
public class TunnelDiodeRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var strokeBrush = renderContext.GetStrokeBrush();
        var strokePen = renderContext.GetStrokePen(2.5);

        var fillBrush = renderContext.GetComponentFillBrush() ?? new SolidColorBrush(Color.FromRgb(24, 32, 45));

        // Leads from pins (-1 to -0.35, +0.35 to +1)
        double triLeft = center.X - u * 0.35;
        double triRight = center.X + u * 0.15;
        double cathodeX = triRight;

        context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(triLeft, center.Y));
        context.DrawLine(strokePen, new Point(cathodeX, center.Y), new Point(center.X + u, center.Y));

        // Triangle
        var geom = new StreamGeometry();
        using (var g = geom.Open())
        {
            g.BeginFigure(new Point(triLeft, center.Y - u * 0.35), true);
            g.LineTo(new Point(triLeft, center.Y + u * 0.35));
            g.LineTo(new Point(triRight, center.Y));
        }
        context.DrawGeometry(fillBrush, strokePen, geom);

        // Tunnel Diode cathode bar with inward tabs:
        // Central vertical cathode line
        context.DrawLine(strokePen, new Point(cathodeX, center.Y - u * 0.4), new Point(cathodeX, center.Y + u * 0.4));
        // Top tab pointing back towards anode (left)
        context.DrawLine(strokePen, new Point(cathodeX, center.Y - u * 0.4), new Point(cathodeX - u * 0.15, center.Y - u * 0.4));
        // Bottom tab pointing forward (or back towards anode)
        context.DrawLine(strokePen, new Point(cathodeX, center.Y + u * 0.4), new Point(cathodeX - u * 0.15, center.Y + u * 0.4));

        // Terminal pads
        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y), 3.2 * z, 3.2 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.2 * z, 3.2 * z);
    }
}
