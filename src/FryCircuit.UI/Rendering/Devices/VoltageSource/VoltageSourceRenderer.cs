using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.VoltageSource;

public class VoltageSourceRenderer : IDeviceRenderer
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

        var vs = device as VoltageSourceDevice;
        bool isSine = vs?.WaveformType == VoltageWaveformType.Sine;

        if (isSine)
        {
            // Smooth sine wave inside circle
            var waveGeom = new StreamGeometry();
            using (var g = waveGeom.Open())
            {
                g.BeginFigure(new Point(center.X - u * 0.3, center.Y), false);
                g.CubicBezierTo(
                    new Point(center.X - u * 0.15, center.Y - u * 0.28),
                    new Point(center.X, center.Y - u * 0.28),
                    new Point(center.X, center.Y));
                g.CubicBezierTo(
                    new Point(center.X, center.Y + u * 0.28),
                    new Point(center.X + u * 0.15, center.Y + u * 0.28),
                    new Point(center.X + u * 0.3, center.Y));
            }
            var wavePen = new Pen(new SolidColorBrush(Color.FromRgb(0, 240, 255)), 2.5 * z)
            {
                LineCap = PenLineCap.Round
            };
            context.DrawGeometry(null, wavePen, waveGeom);
        }
        else
        {
            // DC source: + on top, - on bottom
            var symbolPen = new Pen(strokeBrush, 2.0 * z) { LineCap = PenLineCap.Round };

            // + sign
            context.DrawLine(symbolPen, new Point(center.X - u * 0.15, center.Y - u * 0.22), new Point(center.X + u * 0.15, center.Y - u * 0.22));
            context.DrawLine(symbolPen, new Point(center.X, center.Y - u * 0.37), new Point(center.X, center.Y - u * 0.07));

            // - sign
            context.DrawLine(symbolPen, new Point(center.X - u * 0.15, center.Y + u * 0.22), new Point(center.X + u * 0.15, center.Y + u * 0.22));
        }

        // Terminal solder pads
        context.DrawEllipse(strokeBrush, null, new Point(center.X, center.Y - u), 3.2 * z, 3.2 * z);
        context.DrawEllipse(strokeBrush, null, new Point(center.X, center.Y + u), 3.2 * z, 3.2 * z);
    }
}
