using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Fuse;

public class FuseRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var strokeBrush = renderContext.GetStrokeBrush();
        var strokePen = renderContext.GetStrokePen(2.8);

        // Terminal leads from pins to glass cartridge
        double capH = u * 0.22;
        double bodyH = u * 1.1;
        double bodyW = u * 0.46;
        double halfH = bodyH / 2.0;
        double halfW = bodyW / 2.0;

        context.DrawLine(strokePen, new Point(center.X, center.Y - u), new Point(center.X, center.Y - halfH));
        context.DrawLine(strokePen, new Point(center.X, center.Y + halfH), new Point(center.X, center.Y + u));

        // Glass cartridge body
        var glassRect = new Rect(center.X - halfW, center.Y - halfH, bodyW, bodyH);
        var glassFill = device.IsBlown
            ? new SolidColorBrush(Color.FromArgb(180, 35, 18, 18))
            : new SolidColorBrush(Color.FromArgb(140, 16, 26, 42));
        var glassPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 120, 150, 180)), 1.5 * z);
        context.DrawRectangle(glassFill, glassPen, glassRect, 3.0 * z, 3.0 * z);

        // Metal chrome end caps
        var capFill = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1.0, 0.5, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Color.FromRgb(160, 175, 195), 0.0),
                new GradientStop(Color.FromRgb(240, 245, 255), 0.5),
                new GradientStop(Color.FromRgb(130, 145, 165), 1.0)
            }
        };
        var capPen = new Pen(new SolidColorBrush(Color.FromRgb(100, 115, 135)), 1.2 * z);

        var topCap = new Rect(center.X - halfW - 0.5 * z, center.Y - halfH - 0.5 * z, bodyW + 1.0 * z, capH);
        var bottomCap = new Rect(center.X - halfW - 0.5 * z, center.Y + halfH - capH + 0.5 * z, bodyW + 1.0 * z, capH);
        context.DrawRectangle(capFill, capPen, topCap, 2.0 * z, 2.0 * z);
        context.DrawRectangle(capFill, capPen, bottomCap, 2.0 * z, 2.0 * z);

        // Internal fuse wire
        if (device.IsBlown)
        {
            // Internal burnout soot halo
            var sootBrush = new RadialGradientBrush
            {
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(210, 220, 40, 40), 0.0),
                    new GradientStop(Color.FromArgb(110, 40, 15, 15), 0.5),
                    new GradientStop(Color.FromArgb(0, 15, 10, 10), 1.0)
                }
            };
            context.DrawEllipse(sootBrush, null, center, u * 0.35, u * 0.35);

            // Broken wire with melted droplet beads
            var meltedBrush = new SolidColorBrush(Color.FromRgb(220, 60, 60));
            var wirePen = new Pen(meltedBrush, 2.0 * z) { LineCap = PenLineCap.Round };

            double gap = u * 0.16;
            // Top stub
            context.DrawLine(wirePen, new Point(center.X, center.Y - halfH + capH), new Point(center.X, center.Y - gap));
            context.DrawEllipse(meltedBrush, null, new Point(center.X, center.Y - gap), 2.5 * z, 2.5 * z);

            // Bottom stub
            context.DrawLine(wirePen, new Point(center.X, center.Y + halfH - capH), new Point(center.X, center.Y + gap));
            context.DrawEllipse(meltedBrush, null, new Point(center.X, center.Y + gap), 2.5 * z, 2.5 * z);
        }
        else
        {
            // Intact fuse link wire with signature S-curve neck
            double current = Math.Abs(device.Current);
            var wireBrush = current > 0.01
                ? new SolidColorBrush(Color.FromRgb(255, 215, 110))
                : new SolidColorBrush(Color.FromRgb(200, 210, 225));
            var wirePen = new Pen(wireBrush, 2.2 * z) { LineCap = PenLineCap.Round };

            var wireGeom = new StreamGeometry();
            using (var g = wireGeom.Open())
            {
                g.BeginFigure(new Point(center.X, center.Y - halfH + capH), false);
                g.LineTo(new Point(center.X, center.Y - u * 0.15));
                g.LineTo(new Point(center.X - u * 0.10, center.Y - u * 0.05));
                g.LineTo(new Point(center.X + u * 0.10, center.Y + u * 0.05));
                g.LineTo(new Point(center.X, center.Y + u * 0.15));
                g.LineTo(new Point(center.X, center.Y + halfH - capH));
            }
            context.DrawGeometry(null, wirePen, wireGeom);
        }

        // Terminal solder pads
        double padRadius = (renderContext.IsGlowPass ? 4.0 : 2.5) * z;
        context.DrawEllipse(strokeBrush, null, new Point(center.X, center.Y - u), padRadius, padRadius);
        context.DrawEllipse(strokeBrush, null, new Point(center.X, center.Y + u), padRadius, padRadius);
    }
}
