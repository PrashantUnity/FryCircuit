using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices.Aerospace;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Aerospace;

/// <summary>
/// Industrial Vector CAD renderer for Drone Piezoelectric Alarm Buzzers.
/// Displays cylindrical transducer chamber, center acoustic aperture,
/// dynamic radiating sound wave ripple arcs when active, and dB SPL audio telemetry.
/// </summary>
public class PiezoBuzzerRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double size, bool isSelected, DeviceRenderContext renderContext)
    {
        var buzz = device as PiezoBuzzerDevice;
        double u = size;
        double cx = center.X;
        double cy = center.Y;
        double z = renderContext.Zoom;

        double radius = u * 0.45;

        // Terminal leads: Pin 0 (+) Top, Pin 1 (-) Bottom
        Point pTop = new(cx, cy - u);
        Point pBot = new(cx, cy + u);

        var leadPen = renderContext.GetLeadPen(2.0);
        context.DrawLine(leadPen, pTop, new Point(cx, cy - radius));
        context.DrawLine(leadPen, pBot, new Point(cx, cy + radius));

        // Outer Transducer Chamber Body
        var bodyBrush = renderContext.IsDark
            ? new RadialGradientBrush
            {
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(240, 30, 41, 59), 0.0),
                    new GradientStop(Color.FromArgb(255, 15, 23, 42), 1.0)
                }
            }
            : new RadialGradientBrush
            {
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(240, 248, 250, 252), 0.0),
                    new GradientStop(Color.FromArgb(255, 226, 232, 240), 1.0)
                }
            };
        var bodyPen = renderContext.GetStrokePen(2.2, Color.FromRgb(234, 179, 8), Color.FromRgb(202, 138, 4)); // Golden warning border
        context.DrawEllipse(bodyBrush, bodyPen, center, radius, radius);

        // Center Acoustic Sound Aperture Hole
        var holeBrush = new SolidColorBrush(renderContext.IsDark ? Color.FromRgb(2, 6, 23) : Color.FromRgb(203, 213, 225));
        context.DrawEllipse(holeBrush, null, center, radius * 0.35, radius * 0.35);

        // Radiating Acoustic Waves when Actively Sounding
        if (buzz != null && buzz.IsSounding)
        {
            double phase = buzz.AcousticPhase;
            double maxWaveR = radius * 1.5;

            for (int w = 1; w <= 3; w++)
            {
                double waveR = radius + ((w * 0.35 * radius + phase * 4.0 * z) % maxWaveR);
                byte alpha = (byte)Math.Clamp(200 - (waveR / (radius * 1.6)) * 180, 20, 220);
                var waveColor = renderContext.IsDark ? Color.FromArgb(alpha, 250, 204, 21) : Color.FromArgb(alpha, 217, 119, 6);
                var wavePen = new Pen(new SolidColorBrush(waveColor), 1.5 * z);

                // Right-side sound arc
                var rightArc = new StreamGeometry();
                using (var gctx = rightArc.Open())
                {
                    gctx.BeginFigure(new Point(cx + waveR * 0.8, cy - waveR * 0.5), isFilled: false);
                    gctx.QuadraticBezierTo(new Point(cx + waveR * 1.1, cy), new Point(cx + waveR * 0.8, cy + waveR * 0.5));
                }
                context.DrawGeometry(null, wavePen, rightArc);
            }
        }

        // Terminal solder pads
        context.DrawEllipse(new SolidColorBrush(renderContext.IsDark ? Color.FromRgb(239, 68, 68) : Color.FromRgb(220, 38, 38)), null, pTop, 3.0 * z, 3.0 * z);
        context.DrawEllipse(new SolidColorBrush(renderContext.IsDark ? Color.FromRgb(148, 163, 184) : Color.FromRgb(71, 85, 105)), null, pBot, 3.0 * z, 3.0 * z);
    }
}
