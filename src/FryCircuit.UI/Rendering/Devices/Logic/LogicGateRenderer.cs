using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Logic;

public class LogicGateRenderer : IDeviceRenderer
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

        string gate = (device as LogicGateDevice)?.GateType ?? device.Type.ToLowerInvariant();
        bool isNot = gate == "not";
        bool isOrFamily = gate == "or" || gate == "nor" || gate == "xor" || gate == "xnor";
        bool isXorFamily = gate == "xor" || gate == "xnor";
        bool isInverted = gate.StartsWith("n") || gate == "xnor";

        if (isNot)
        {
            // NOT triangle with tangent inversion bubble
            double triLeft = center.X - u * 0.45;
            double triRight = center.X + u * 0.18;
            double rBubble = u * 0.085;

            var geom = new StreamGeometry();
            using (var g = geom.Open())
            {
                g.BeginFigure(new Point(triLeft, center.Y - u * 0.5), true);
                g.LineTo(new Point(triLeft, center.Y + u * 0.5));
                g.LineTo(new Point(triRight, center.Y));
            }
            context.DrawGeometry(fillBrush, strokePen, geom);

            // Inversion bubble
            context.DrawEllipse(fillBrush, strokePen, new Point(triRight + rBubble, center.Y), rBubble, rBubble);

            // Input & output leads
            context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(triLeft, center.Y));
            context.DrawLine(strokePen, new Point(triRight + 2.0 * rBubble, center.Y), new Point(center.X + u, center.Y));

            // Terminal pads
            context.DrawEllipse(strokeBrush, null, new Point(center.X - u, center.Y), 3.2 * z, 3.2 * z);
            context.DrawEllipse(strokeBrush, null, new Point(center.X + u, center.Y), 3.2 * z, 3.2 * z);
        }
        else
        {
            // 2-input gate (AND / NAND / OR / NOR / XOR / XNOR)
            double gateLeft = center.X - u * 0.45;
            double apexX = isInverted ? center.X + u * 0.27 : center.X + u * 0.45;
            double rBubble = u * 0.085;
            double inY = u * 0.32;
            double jogX = center.X - u * 0.65;

            var bodyGeom = new StreamGeometry();
            if (!isOrFamily)
            {
                // AND / NAND standard D-shape with semicircle nose
                double noseCenterX = apexX - u * 0.5;
                using (var g = bodyGeom.Open())
                {
                    g.BeginFigure(new Point(gateLeft, center.Y - u * 0.5), true);
                    g.LineTo(new Point(noseCenterX, center.Y - u * 0.5));
                    g.ArcTo(new Point(noseCenterX, center.Y + u * 0.5), new Size(u * 0.5, u * 0.5), 0, false, SweepDirection.Clockwise);
                    g.LineTo(new Point(gateLeft, center.Y + u * 0.5));
                }
            }
            else
            {
                // OR / NOR / XOR / XNOR pointed curved body with concave input back
                using (var g = bodyGeom.Open())
                {
                    g.BeginFigure(new Point(gateLeft, center.Y - u * 0.5), true);
                    // Concave back curve
                    g.CubicBezierTo(
                        new Point(gateLeft + u * 0.16, center.Y - u * 0.25),
                        new Point(gateLeft + u * 0.16, center.Y + u * 0.25),
                        new Point(gateLeft, center.Y + u * 0.5));
                    // Bottom edge to pointed apex
                    g.CubicBezierTo(
                        new Point(gateLeft + (apexX - gateLeft) * 0.45, center.Y + u * 0.5),
                        new Point(apexX - u * 0.12, center.Y + u * 0.16),
                        new Point(apexX, center.Y));
                    // Pointed apex to top edge
                    g.CubicBezierTo(
                        new Point(apexX - u * 0.12, center.Y - u * 0.16),
                        new Point(gateLeft + (apexX - gateLeft) * 0.45, center.Y - u * 0.5),
                        new Point(gateLeft, center.Y - u * 0.5));
                }
            }
            context.DrawGeometry(fillBrush, strokePen, bodyGeom);

            // XOR / XNOR extra curved input bar
            double xorOffset = u * 0.14;
            if (isXorFamily)
            {
                var xorGeom = new StreamGeometry();
                using (var xg = xorGeom.Open())
                {
                    xg.BeginFigure(new Point(gateLeft - xorOffset, center.Y - u * 0.5), false);
                    xg.CubicBezierTo(
                        new Point(gateLeft - xorOffset + u * 0.16, center.Y - u * 0.25),
                        new Point(gateLeft - xorOffset + u * 0.16, center.Y + u * 0.25),
                        new Point(gateLeft - xorOffset, center.Y + u * 0.5));
                }
                context.DrawGeometry(null, strokePen, xorGeom);
            }

            // Output inversion bubble & lead
            if (isInverted)
            {
                context.DrawEllipse(fillBrush, strokePen, new Point(apexX + rBubble, center.Y), rBubble, rBubble);
                context.DrawLine(strokePen, new Point(apexX + 2.0 * rBubble, center.Y), new Point(center.X + u, center.Y));
            }
            else
            {
                context.DrawLine(strokePen, new Point(apexX, center.Y), new Point(center.X + u, center.Y));
            }

            // Determine where input leads attach
            double leadAttachX = gateLeft;
            if (isXorFamily)
            {
                leadAttachX = gateLeft - xorOffset + u * 0.08;
            }
            else if (isOrFamily)
            {
                leadAttachX = gateLeft + u * 0.08;
            }

            // Pin 0 (Input A at (-1, -1))
            context.DrawLine(strokePen, new Point(center.X - u, center.Y - u), new Point(jogX, center.Y - u));
            context.DrawLine(strokePen, new Point(jogX, center.Y - u), new Point(jogX, center.Y - inY));
            context.DrawLine(strokePen, new Point(jogX, center.Y - inY), new Point(leadAttachX, center.Y - inY));

            // Pin 1 (Input B at (-1, +1))
            context.DrawLine(strokePen, new Point(center.X - u, center.Y + u), new Point(jogX, center.Y + u));
            context.DrawLine(strokePen, new Point(jogX, center.Y + u), new Point(jogX, center.Y + inY));
            context.DrawLine(strokePen, new Point(jogX, center.Y + inY), new Point(leadAttachX, center.Y + inY));

            // Terminal pads
            context.DrawEllipse(strokeBrush, null, new Point(center.X - u, center.Y - u), 3.2 * z, 3.2 * z);
            context.DrawEllipse(strokeBrush, null, new Point(center.X - u, center.Y + u), 3.2 * z, 3.2 * z);
            context.DrawEllipse(strokeBrush, null, new Point(center.X + u, center.Y), 3.2 * z, 3.2 * z);
        }
    }
}
