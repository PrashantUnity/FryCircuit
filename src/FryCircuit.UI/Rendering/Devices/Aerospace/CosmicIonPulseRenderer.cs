using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Aerospace;

/// <summary>
/// Professional Aerospace CAD renderer for Cosmic Ion Strike Radiation Transient Source.
/// Displays high-energy cosmic ray trajectory vector, charge injection symbol, and terminals.
/// </summary>
public class CosmicIonPulseRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double size, bool isSelected, DeviceRenderContext renderContext)
    {
        var ion = device as CosmicIonPulseDevice;
        var strokePen = new Pen(new SolidColorBrush(Color.FromRgb(255, 140, 0)), 2.0 * renderContext.Zoom);
        var pulsePen = new Pen(new SolidColorBrush(Color.FromRgb(255, 230, 80)), 2.5 * renderContext.Zoom);
        var bgBrush = new SolidColorBrush(Color.FromArgb(190, 28, 20, 10));

        double r = size * 0.38;
        var circleRect = new Rect(center.X - r, center.Y - r, r * 2, r * 2);

        // Terminal leads
        context.DrawLine(strokePen, new Point(center.X, center.Y - size), new Point(center.X, center.Y - r));
        context.DrawLine(strokePen, new Point(center.X, center.Y + r), new Point(center.X, center.Y + size));

        // Source circle
        context.DrawEllipse(bgBrush, strokePen, center, r, r);

        // Cosmic Ray Ion Strike Lightning / Trajectory Arrow
        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            ctx.BeginFigure(new Point(center.X - r * 0.45, center.Y - r * 0.45), isFilled: false);
            ctx.LineTo(new Point(center.X - r * 0.1, center.Y));
            ctx.LineTo(new Point(center.X + r * 0.1, center.Y - r * 0.15));
            ctx.LineTo(new Point(center.X + r * 0.45, center.Y + r * 0.45));
        }
        context.DrawGeometry(null, pulsePen, geom);

        // Arrowhead pointing into node
        var headGeom = new StreamGeometry();
        using (var ctx = headGeom.Open())
        {
            ctx.BeginFigure(new Point(center.X + r * 0.45, center.Y + r * 0.45), isFilled: true);
            ctx.LineTo(new Point(center.X + r * 0.2, center.Y + r * 0.42));
            ctx.LineTo(new Point(center.X + r * 0.42, center.Y + r * 0.2));
            ctx.EndFigure(isClosed: true);
        }
        context.DrawGeometry(new SolidColorBrush(Color.FromRgb(255, 230, 80)), null, headGeom);

        // Terminals
        var dotBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        context.DrawEllipse(dotBrush, null, new Point(center.X, center.Y - size), 2.5 * renderContext.Zoom, 2.5 * renderContext.Zoom);
        context.DrawEllipse(dotBrush, null, new Point(center.X, center.Y + size), 2.5 * renderContext.Zoom, 2.5 * renderContext.Zoom);
    }
}
