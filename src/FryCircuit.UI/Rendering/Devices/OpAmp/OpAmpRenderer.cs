using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.OpAmp;

public class OpAmpRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var strokeBrush = renderContext.GetStrokeBrush();
        var strokePen = renderContext.GetStrokePen(2.8);

        var fillBrush = renderContext.GetComponentFillBrush();

        // Triangle body: pointing to the right (+X)
        double triLeft = center.X - u * 0.5;
        double triRight = center.X + u * 0.7;
        double triTop = center.Y - u * 1.4;
        double triBottom = center.Y + u * 1.4;

        var geom = new StreamGeometry();
        using (var g = geom.Open())
        {
            g.BeginFigure(new Point(triLeft, triTop), true);
            g.LineTo(new Point(triLeft, triBottom));
            g.LineTo(new Point(triRight, center.Y));
        }
        context.DrawGeometry(fillBrush, strokePen, geom);

        // Terminal leads - purely orthogonal/horizontal matching grid pins
        // Pin 0: Inverting input (-) at (-1, -1)
        context.DrawLine(strokePen, new Point(center.X - u, center.Y - u), new Point(triLeft, center.Y - u));
        // Pin 1: Non-inverting input (+) at (-1, 1)
        context.DrawLine(strokePen, new Point(center.X - u, center.Y + u), new Point(triLeft, center.Y + u));
        // Pin 2: Output at (1, 0)
        context.DrawLine(strokePen, new Point(triRight, center.Y), new Point(center.X + u, center.Y));

        // - and + signs inside opamp
        var symColor = renderContext.IsDark ? Color.FromRgb(160, 200, 240) : Color.FromRgb(71, 85, 105);
        var symPen = new Pen(new SolidColorBrush(symColor), 2.0 * z) { LineCap = PenLineCap.Round };

        // - sign (aligned with inverting pin 0 at Y = center.Y - u)
        context.DrawLine(symPen, new Point(triLeft + u * 0.14, center.Y - u), new Point(triLeft + u * 0.34, center.Y - u));

        // + sign (aligned with non-inverting pin 1 at Y = center.Y + u)
        context.DrawLine(symPen, new Point(triLeft + u * 0.14, center.Y + u), new Point(triLeft + u * 0.34, center.Y + u));
        context.DrawLine(symPen, new Point(triLeft + u * 0.24, center.Y + u - u * 0.1), new Point(triLeft + u * 0.24, center.Y + u + u * 0.1));

        // Terminal solder pads
        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y - u), 3.2 * z, 3.2 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y + u), 3.2 * z, 3.2 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.2 * z, 3.2 * z);
    }
}
