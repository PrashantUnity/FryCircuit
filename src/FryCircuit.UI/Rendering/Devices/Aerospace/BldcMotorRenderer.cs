using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices.Aerospace;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Aerospace;

/// <summary>
/// Industrial Vector CAD renderer for 3-Phase BLDC Drone Outrunner Motors.
/// Features circular stator bell housing, 3-phase terminal wire leads,
/// central rotating rotor with carbon-fiber multi-rotor propeller, and real-time thrust / RPM telemetry.
/// </summary>
public class BldcMotorRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double size, bool isSelected, DeviceRenderContext renderContext)
    {
        var bldc = device as BldcMotorDevice;
        double u = size;
        double cx = center.X;
        double cy = center.Y;
        double z = renderContext.Zoom;

        double radius = u * 0.55;

        // 3 Phase Terminals on Left: U (-u, -u), V (-u, 0), W (-u, u)
        Point pU = new(cx - u, cy - u);
        Point pV = new(cx - u, cy);
        Point pW = new(cx - u, cy + u);

        var leadPenU = new Pen(new SolidColorBrush(Color.FromRgb(244, 63, 94)), 2.0 * z);  // Red (U)
        var leadPenV = new Pen(new SolidColorBrush(Color.FromRgb(250, 204, 21)), 2.0 * z); // Yellow (V)
        var leadPenW = new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 2.0 * z); // Blue (W)

        // Connect terminal leads to stator ring
        context.DrawLine(leadPenU, pU, new Point(cx - radius * 0.707, cy - radius * 0.707));
        context.DrawLine(leadPenV, pV, new Point(cx - radius, cy));
        context.DrawLine(leadPenW, pW, new Point(cx - radius * 0.707, cy + radius * 0.707));

        // Outer Stator Bell Housing
        var statorBrush = renderContext.IsDark
            ? new RadialGradientBrush
            {
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(240, 20, 28, 42), 0.0),
                    new GradientStop(Color.FromArgb(255, 11, 15, 25), 1.0)
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
        var statorPen = renderContext.GetStrokePen(2.2, Color.FromRgb(148, 163, 184), Color.FromRgb(71, 85, 105));
        context.DrawEllipse(statorBrush, statorPen, center, radius, radius);

        // Stator Copper Coils (12 Stator Teeth)
        int teeth = 12;
        var copperBrush = new SolidColorBrush(renderContext.IsDark ? Color.FromRgb(217, 119, 6) : Color.FromRgb(180, 83, 9)); // Copper amber
        for (int i = 0; i < teeth; i++)
        {
            double a = i * 2.0 * Math.PI / teeth;
            double tx = cx + radius * 0.72 * Math.Cos(a);
            double ty = cy + radius * 0.72 * Math.Sin(a);
            context.DrawEllipse(copperBrush, null, new Point(tx, ty), 2.2 * z, 2.2 * z);
        }

        // Dynamic Spinning 3-Blade Propeller (Mechanical Angle)
        double propAngle = bldc?.MechanicalAngleRadians ?? 0.0;
        int propBlades = 3;
        double propLength = radius * 1.15;
        double propHub = radius * 0.32;

        var propBrush = (bldc != null && bldc.CurrentRpm > 50)
            ? (renderContext.IsDark ? new SolidColorBrush(Color.FromArgb(220, 20, 184, 166)) : new SolidColorBrush(Color.FromArgb(220, 13, 148, 136))) // Active Teal
            : (renderContext.IsDark ? new SolidColorBrush(Color.FromArgb(170, 71, 85, 105)) : new SolidColorBrush(Color.FromArgb(180, 148, 163, 184)));
        var propPen = new Pen(new SolidColorBrush(renderContext.IsDark ? Color.FromRgb(226, 232, 240) : Color.FromRgb(51, 65, 85)), 1.2 * z);

        for (int i = 0; i < propBlades; i++)
        {
            double a = propAngle + (i * 2.0 * Math.PI / propBlades);
            var bladeGeom = new StreamGeometry();
            using (var gctx = bladeGeom.Open())
            {
                Point r0 = new(cx + propHub * Math.Cos(a - 0.2), cy + propHub * Math.Sin(a - 0.2));
                Point tip = new(cx + propLength * Math.Cos(a), cy + propLength * Math.Sin(a));
                Point r1 = new(cx + propHub * Math.Cos(a + 0.2), cy + propHub * Math.Sin(a + 0.2));

                gctx.BeginFigure(r0, isFilled: true);
                gctx.QuadraticBezierTo(new Point(cx + propLength * 0.6 * Math.Cos(a + 0.15), cy + propLength * 0.6 * Math.Sin(a + 0.15)), tip);
                gctx.QuadraticBezierTo(new Point(cx + propLength * 0.6 * Math.Cos(a - 0.1), cy + propLength * 0.6 * Math.Sin(a - 0.1)), r1);
                gctx.EndFigure(isClosed: true);
            }
            context.DrawGeometry(propBrush, propPen, bladeGeom);
        }

        // Center Aluminum Rotor Cap / Propeller Nut
        var capBrush = renderContext.IsDark
            ? new RadialGradientBrush
            {
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromRgb(226, 232, 240), 0.0),
                    new GradientStop(Color.FromRgb(71, 85, 105), 1.0)
                }
            }
            : new RadialGradientBrush
            {
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromRgb(255, 255, 255), 0.0),
                    new GradientStop(Color.FromRgb(203, 213, 225), 1.0)
                }
            };
        var capBorderPen = new Pen(new SolidColorBrush(renderContext.IsDark ? Color.FromRgb(15, 23, 42) : Color.FromRgb(100, 116, 139)), 1.5 * z);
        context.DrawEllipse(capBrush, capBorderPen, center, propHub, propHub);

        // Phase Terminal Solder Pads
        context.DrawEllipse(new SolidColorBrush(renderContext.IsDark ? Color.FromRgb(244, 63, 94) : Color.FromRgb(225, 29, 72)), null, pU, 3.0 * z, 3.0 * z);
        context.DrawEllipse(new SolidColorBrush(renderContext.IsDark ? Color.FromRgb(250, 204, 21) : Color.FromRgb(202, 138, 4)), null, pV, 3.0 * z, 3.0 * z);
        context.DrawEllipse(new SolidColorBrush(renderContext.IsDark ? Color.FromRgb(56, 189, 248) : Color.FromRgb(2, 132, 199)), null, pW, 3.0 * z, 3.0 * z);
    }
}
