using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices.Aerospace;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Aerospace;

/// <summary>
/// Industrial Vector CAD renderer for DC Brushless Cooling Fans and Drone Propeller Blowers.
/// Features aerodynamic duct housing, mounting eyelets, center hub, dynamically spinning
/// impeller blades, airflow motion vectors, and live RPM / CFM telemetry badges.
/// </summary>
public class FanRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double size, bool isSelected, DeviceRenderContext renderContext)
    {
        var fan = device as FanDevice;
        double u = size;
        double cx = center.X;
        double cy = center.Y;
        double z = renderContext.Zoom;

        var leadBrush = renderContext.GetStrokeBrush(Color.FromRgb(200, 215, 235));
        var leadPen = new Pen(leadBrush, 2.2 * z);

        double radius = u * 0.52;

        // Terminal Leads: Pin 0 (+) Left, Pin 1 (-) Right, Pin 2 (TACH) Bottom
        Point pLeft = new(cx - u, cy);
        Point pRight = new(cx + u, cy);
        Point pTach = new(cx, cy + u);

        context.DrawLine(leadPen, pLeft, new Point(cx - radius, cy));
        context.DrawLine(leadPen, pRight, new Point(cx + radius, cy));
        context.DrawLine(leadPen, pTach, new Point(cx, cy + radius));

        // Outer Industrial Duct Frame (Rounded Square with corner mounting eyelets)
        double frameSize = radius * 1.9;
        var frameRect = new Rect(cx - frameSize / 2.0, cy - frameSize / 2.0, frameSize, frameSize);
        var frameBrush = new SolidColorBrush(Color.FromArgb(235, 15, 23, 38));
        var frameStrokePen = new Pen(renderContext.GetStrokeBrush(Color.FromRgb(56, 189, 248)), 1.8 * z);
        context.DrawRectangle(frameBrush, frameStrokePen, frameRect, 6.0 * z, 6.0 * z);

        // 4 Corner Screw Eyelets
        double eyeletOff = frameSize * 0.38;
        var eyeletPen = new Pen(new SolidColorBrush(Color.FromRgb(100, 116, 139)), 1.2 * z);
        context.DrawEllipse(null, eyeletPen, new Point(cx - eyeletOff, cy - eyeletOff), 2.2 * z, 2.2 * z);
        context.DrawEllipse(null, eyeletPen, new Point(cx + eyeletOff, cy - eyeletOff), 2.2 * z, 2.2 * z);
        context.DrawEllipse(null, eyeletPen, new Point(cx - eyeletOff, cy + eyeletOff), 2.2 * z, 2.2 * z);
        context.DrawEllipse(null, eyeletPen, new Point(cx + eyeletOff, cy + eyeletOff), 2.2 * z, 2.2 * z);

        // Circular Intake Duct Shroud
        var shroudBrush = new RadialGradientBrush
        {
            GradientStops = new GradientStops
            {
                new GradientStop(Color.FromArgb(240, 24, 34, 52), 0.0),
                new GradientStop(Color.FromArgb(255, 10, 15, 26), 1.0)
            }
        };
        context.DrawEllipse(shroudBrush, new Pen(new SolidColorBrush(Color.FromArgb(160, 148, 163, 184)), 1.5 * z), center, radius, radius);

        // Dynamic Spinning Impeller Blades (7 Aerodynamic Curved Blades)
        double angle = fan?.RotorAngleRadians ?? 0.0;
        int bladeCount = 7;
        double bladeLength = radius * 0.82;
        double hubRadius = radius * 0.28;

        var bladeBrush = (fan != null && fan.CurrentRpm > 100)
            ? new SolidColorBrush(Color.FromArgb(200, 56, 189, 248))
            : new SolidColorBrush(Color.FromArgb(160, 148, 163, 184));
        var bladePen = new Pen(new SolidColorBrush(Color.FromRgb(226, 232, 240)), 1.0 * z);

        for (int i = 0; i < bladeCount; i++)
        {
            double bladeAngle = angle + (i * 2.0 * Math.PI / bladeCount);
            double cos = Math.Cos(bladeAngle);
            double sin = Math.Sin(bladeAngle);

            // Curved airfoil blade geometry
            var bladeGeom = new StreamGeometry();
            using (var gctx = bladeGeom.Open())
            {
                Point root = new(cx + hubRadius * cos, cy + hubRadius * sin);
                double tipCos = Math.Cos(bladeAngle + 0.35);
                double tipSin = Math.Sin(bladeAngle + 0.35);
                Point tip = new(cx + bladeLength * tipCos, cy + bladeLength * tipSin);

                double midCos = Math.Cos(bladeAngle + 0.18);
                double midSin = Math.Sin(bladeAngle + 0.18);
                Point mid = new(cx + bladeLength * 0.65 * midCos, cy + bladeLength * 0.65 * midSin);

                gctx.BeginFigure(root, isFilled: true);
                gctx.LineTo(mid);
                gctx.LineTo(tip);
                gctx.LineTo(new Point(cx + hubRadius * Math.Cos(bladeAngle + 0.2), cy + hubRadius * Math.Sin(bladeAngle + 0.2)));
                gctx.EndFigure(isClosed: true);
            }
            context.DrawGeometry(bladeBrush, bladePen, bladeGeom);
        }

        // Central Motor Hub with bearing ring
        var hubBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59));
        var hubPen = new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 1.8 * z);
        context.DrawEllipse(hubBrush, hubPen, center, hubRadius, hubRadius);
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(148, 163, 184)), null, center, hubRadius * 0.4, hubRadius * 0.4);

        // Airflow velocity vector arcs when actively running
        if (fan != null && fan.CurrentRpm > 200)
        {
            var swirlPen = new Pen(new SolidColorBrush(Color.FromArgb(90, 56, 189, 248)), 1.5 * z)
            {
                DashStyle = DashStyle.Dash
            };
            context.DrawEllipse(null, swirlPen, center, radius * 0.6, radius * 0.6);
        }

        // Terminal solder pads
        var padBrush = new SolidColorBrush(Color.FromRgb(240, 246, 255));
        context.DrawEllipse(padBrush, null, pLeft, 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, pRight, 3.0 * z, 3.0 * z);
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(250, 204, 21)), null, pTach, 2.6 * z, 2.6 * z);
    }
}
