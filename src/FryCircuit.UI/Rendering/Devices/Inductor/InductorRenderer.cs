using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Inductor;

public class InductorRenderer : IDeviceRenderer
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

        // Lead lines
        context.DrawLine(strokePen, new Point(center.X, center.Y - u), new Point(center.X, center.Y - u * 0.6));

        // Coils
        for (int i = 0; i < 3; i++)
        {
            double loopY = center.Y - u * 0.4 + i * u * 0.4;
            var arcGeom = new StreamGeometry();
            using (var g = arcGeom.Open())
            {
                g.BeginFigure(new Point(center.X, loopY - u * 0.2), false);
                g.ArcTo(new Point(center.X, loopY + u * 0.2), new Size(u * 0.35, u * 0.2), 0, false, SweepDirection.Clockwise);
            }
            context.DrawGeometry(null, strokePen, arcGeom);
        }

        context.DrawLine(strokePen, new Point(center.X, center.Y + u * 0.6), new Point(center.X, center.Y + u));

        // Terminal solder pads
        context.DrawEllipse(strokeBrush, null, new Point(center.X, center.Y - u), 3.2 * z, 3.2 * z);
        context.DrawEllipse(strokeBrush, null, new Point(center.X, center.Y + u), 3.2 * z, 3.2 * z);
    }
}
