using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Diode;

public class DiodeRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var strokeBrush = renderContext.GetStrokeBrush();
        var strokePen = renderContext.GetStrokePen(2.8);

        bool isLed = device is LedDevice || device.Type == "led";
        string hexColor = (device as LedDevice)?.ColorHex ?? "#00E676";
        var ledColor = Color.Parse(hexColor);
        bool isEmitting = isLed && device.Current > 0.0005;

        // Radiant multi-stop emission halo behind LED when conducting
        if (isEmitting)
        {
            var haloBrush = new RadialGradientBrush
            {
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(170, ledColor.R, ledColor.G, ledColor.B), 0.0),
                    new GradientStop(Color.FromArgb(90, ledColor.R, ledColor.G, ledColor.B), 0.45),
                    new GradientStop(Color.FromArgb(0, ledColor.R, ledColor.G, ledColor.B), 1.0)
                }
            };
            context.DrawEllipse(haloBrush, null, center, u * 1.3, u * 1.3);
        }

        var fillBrush = isEmitting
            ? new SolidColorBrush(Color.FromArgb(230, ledColor.R, ledColor.G, ledColor.B))
            : isLed
                ? new SolidColorBrush(Color.FromArgb(55, ledColor.R, ledColor.G, ledColor.B))
                : new SolidColorBrush(Color.FromRgb(30, 42, 60));

        // Leads from pins to triangle
        context.DrawLine(strokePen, new Point(center.X, center.Y - u), new Point(center.X, center.Y - u * 0.35));
        context.DrawLine(strokePen, new Point(center.X, center.Y + u * 0.35), new Point(center.X, center.Y + u));

        // Diode triangle (pointing down toward cathode)
        var triGeom = new StreamGeometry();
        using (var g = triGeom.Open())
        {
            g.BeginFigure(new Point(center.X - u * 0.38, center.Y - u * 0.35), true);
            g.LineTo(new Point(center.X + u * 0.38, center.Y - u * 0.35));
            g.LineTo(new Point(center.X, center.Y + u * 0.35));
        }
        context.DrawGeometry(fillBrush, strokePen, triGeom);

        // Cathode bar
        double barY = center.Y + u * 0.35;
        context.DrawLine(strokePen, new Point(center.X - u * 0.38, barY), new Point(center.X + u * 0.38, barY));

        // Zener cathode wings
        if (device is ZenerDiodeDevice || device.Type == "zener")
        {
            context.DrawLine(strokePen, new Point(center.X - u * 0.38, barY), new Point(center.X - u * 0.38, barY - u * 0.18));
            context.DrawLine(strokePen, new Point(center.X + u * 0.38, barY), new Point(center.X + u * 0.38, barY + u * 0.18));
        }

        // LED photon rays
        if (isLed)
        {
            // Two diagonal emission arrows
            var rayPen = new Pen(new SolidColorBrush(isEmitting ? ledColor : Color.FromArgb(170, ledColor.R, ledColor.G, ledColor.B)), 2.2 * z)
            {
                LineCap = PenLineCap.Round
            };

            for (int r = 0; r < 2; r++)
            {
                double offX = u * 0.45 + r * u * 0.22;
                double offY = -u * 0.15 + r * u * 0.22;

                Point rayStart = new Point(center.X + offX, center.Y + offY);
                Point rayEnd = new Point(center.X + offX + u * 0.26, center.Y + offY - u * 0.26);
                context.DrawLine(rayPen, rayStart, rayEnd);

                // Arrow head
                context.DrawLine(rayPen, rayEnd, new Point(rayEnd.X - u * 0.1, rayEnd.Y));
                context.DrawLine(rayPen, rayEnd, new Point(rayEnd.X, rayEnd.Y + u * 0.1));
            }
        }

        // Terminal solder pads
        double padRadius = (renderContext.IsGlowPass ? 4.8 : 3.2) * z;
        context.DrawEllipse(strokeBrush, null, new Point(center.X, center.Y - u), padRadius, padRadius);
        context.DrawEllipse(strokeBrush, null, new Point(center.X, center.Y + u), padRadius, padRadius);
    }
}
