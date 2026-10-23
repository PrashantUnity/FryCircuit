using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Potentiometer;

public class PotentiometerRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var strokeBrush = renderContext.GetStrokeBrush();
        var strokePen = renderContext.GetStrokePen(2.8);

        // Resistor body
        double rX = center.X - u * 0.2;
        var geom = new StreamGeometry();
        using (var g = geom.Open())
        {
            g.BeginFigure(new Point(center.X, center.Y - u), false);
            g.LineTo(new Point(rX, center.Y - u * 0.55));
            g.LineTo(new Point(rX - u * 0.25, center.Y - u * 0.37));
            g.LineTo(new Point(rX + u * 0.25, center.Y - u * 0.18));
            g.LineTo(new Point(rX - u * 0.25, center.Y + 0.0));
            g.LineTo(new Point(rX + u * 0.25, center.Y + u * 0.18));
            g.LineTo(new Point(rX - u * 0.25, center.Y + u * 0.37));
            g.LineTo(new Point(rX, center.Y + u * 0.55));
            g.LineTo(new Point(center.X, center.Y + u));
        }
        context.DrawGeometry(null, strokePen, geom);

        // Wiper arrow (Pin 1 at (1, 0))
        var wiperColor = renderContext.IsDark ? Color.FromRgb(0, 230, 118) : Color.FromRgb(5, 150, 105);
        var wiperPen = new Pen(new SolidColorBrush(wiperColor), 2.6 * z)
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        double potWiper = (device as PotentiometerDevice)?.Wiper ?? 0.5;
        double wiperY = center.Y - u * 0.4 + potWiper * u * 0.8;
        double leadCornerX = center.X + u * 0.45;

        // Orthogonal lead: horizontal from pin to corner, vertical to wiper level, horizontal into resistor body
        context.DrawLine(wiperPen, new Point(center.X + u, center.Y), new Point(leadCornerX, center.Y));
        context.DrawLine(wiperPen, new Point(leadCornerX, center.Y), new Point(leadCornerX, wiperY));
        context.DrawLine(wiperPen, new Point(leadCornerX, wiperY), new Point(rX + u * 0.05, wiperY));

        // Arrowhead pointing into resistor body
        context.DrawLine(wiperPen, new Point(rX + u * 0.18, wiperY - u * 0.12), new Point(rX + u * 0.05, wiperY));
        context.DrawLine(wiperPen, new Point(rX + u * 0.18, wiperY + u * 0.12), new Point(rX + u * 0.05, wiperY));

        // Terminal solder pads
        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y - u), 3.2 * z, 3.2 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y + u), 3.2 * z, 3.2 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.2 * z, 3.2 * z);
    }
}
