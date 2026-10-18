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

        var strokeBrush = renderContext.GetStrokeBrush();
        var strokePen = renderContext.GetStrokePen(2.8);

        var fillBrush = renderContext.IsGlowPass ? null : new SolidColorBrush(Color.FromArgb(220, 15, 23, 42));

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
            var wavePen = new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 2.0 * z)
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
        double padRadius = (renderContext.IsGlowPass ? 4.0 : 2.5) * z;
        context.DrawEllipse(strokeBrush, null, new Point(center.X, center.Y - u), padRadius, padRadius);
        context.DrawEllipse(strokeBrush, null, new Point(center.X, center.Y + u), padRadius, padRadius);
    }
}
