using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Resistor;

public class ResistorRenderer : IDeviceRenderer
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

        var geom = new StreamGeometry();
        using (var g = geom.Open())
        {
            g.BeginFigure(new Point(center.X, center.Y - u), false);
            g.LineTo(new Point(center.X, center.Y - u * 0.55));
            g.LineTo(new Point(center.X - u * 0.32, center.Y - u * 0.37));
            g.LineTo(new Point(center.X + u * 0.32, center.Y - u * 0.18));
            g.LineTo(new Point(center.X - u * 0.32, center.Y + 0.0));
            g.LineTo(new Point(center.X + u * 0.32, center.Y + u * 0.18));
            g.LineTo(new Point(center.X - u * 0.32, center.Y + u * 0.37));
            g.LineTo(new Point(center.X, center.Y + u * 0.55));
            g.LineTo(new Point(center.X, center.Y + u));
        }
        context.DrawGeometry(null, strokePen, geom);

        // Terminal solder pads
        context.DrawEllipse(strokeBrush, null, new Point(center.X, center.Y - u), 3.2 * z, 3.2 * z);
        context.DrawEllipse(strokeBrush, null, new Point(center.X, center.Y + u), 3.2 * z, 3.2 * z);
    }
}
