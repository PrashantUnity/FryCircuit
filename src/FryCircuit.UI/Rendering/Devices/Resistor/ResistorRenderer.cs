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

        var strokeBrush = renderContext.GetStrokeBrush();
        var strokePen = renderContext.GetStrokePen(2.8);

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
        double padRadius = (renderContext.IsGlowPass ? 4.0 : 2.5) * z;
        context.DrawEllipse(strokeBrush, null, new Point(center.X, center.Y - u), padRadius, padRadius);
        context.DrawEllipse(strokeBrush, null, new Point(center.X, center.Y + u), padRadius, padRadius);
    }
}
