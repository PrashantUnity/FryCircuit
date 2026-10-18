using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Lamp;

public class LampRenderer : IDeviceRenderer
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

        // Warm radiance glow when powered
        double power = device.Power;
        if (power > 0.01)
        {
            byte alpha = (byte)Math.Clamp(power * 25, 40, 180);
            var glowBrush = new RadialGradientBrush
            {
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(alpha, 255, 215, 0), 0.0),
                    new GradientStop(Color.FromArgb(0, 255, 170, 0), 1.0)
                }
            };
            context.DrawEllipse(glowBrush, null, center, radius * 2.2, radius * 2.2);
        }

        // Bulb circle
        context.DrawEllipse(fillBrush, strokePen, center, radius, radius);

        // 'X' cross filament
        double crossOff = radius * 0.65;
        var filamentBrush = power > 0.01
            ? new SolidColorBrush(Color.FromRgb(255, 230, 100))
            : strokeBrush;
        var filamentPen = new Pen(filamentBrush, 2.2 * z) { LineCap = PenLineCap.Round };

        context.DrawLine(filamentPen, new Point(center.X - crossOff, center.Y - crossOff), new Point(center.X + crossOff, center.Y + crossOff));
        context.DrawLine(filamentPen, new Point(center.X - crossOff, center.Y + crossOff), new Point(center.X + crossOff, center.Y - crossOff));

        // Terminal solder pads
        context.DrawEllipse(strokeBrush, null, new Point(center.X, center.Y - u), 3.2 * z, 3.2 * z);
        context.DrawEllipse(strokeBrush, null, new Point(center.X, center.Y + u), 3.2 * z, 3.2 * z);
    }
}
