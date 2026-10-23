using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices.Control;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Control;

#region 1. Source Renderers (Ramp, Constant, Noise, Pulse)
public class RampSourceRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;
        var strokePen = renderContext.GetStrokePen(2.0);
        var fillBrush = renderContext.GetComponentFillBrush();

        double r = 0.8 * u;
        context.DrawEllipse(fillBrush, strokePen, center, r, r);

        // Terminal leads
        context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(center.X - r, center.Y));
        context.DrawLine(strokePen, new Point(center.X + r, center.Y), new Point(center.X + u, center.Y));

        // Draw ramp waveform inside
        var wavePen = new Pen(renderContext.IsDark ? Brushes.Cyan : Brushes.Teal, 1.8 * z);
        context.DrawLine(wavePen, new Point(center.X - r * 0.5, center.Y + r * 0.4), new Point(center.X + r * 0.5, center.Y - r * 0.4));

        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.0 * z, 3.0 * z);
    }
}

public class ConstantSourceRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;
        var strokePen = renderContext.GetStrokePen(2.0);
        var fillBrush = renderContext.GetComponentFillBrush();

        double boxW = 1.4 * u;
        double boxH = 1.2 * u;
        var rect = new Rect(center.X - boxW / 2.0, center.Y - boxH / 2.0, boxW, boxH);
        context.DrawRectangle(fillBrush, strokePen, rect, 4.0 * z, 4.0 * z);

        context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(rect.Left, center.Y));
        context.DrawLine(strokePen, new Point(rect.Right, center.Y), new Point(center.X + u, center.Y));

        string label = (device is ConstantSourceDevice c) ? $"{c.Value:G3}" : "K";
        var textBrush = renderContext.IsDark ? Brushes.White : Brushes.Black;
        var text = new FormattedText(
            label,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Bold),
            11.0 * z,
            textBrush
        );
        context.DrawText(text, new Point(center.X - text.Width / 2.0, center.Y - text.Height / 2.0));

        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.0 * z, 3.0 * z);
    }
}

public class NoiseSourceRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;
        var strokePen = renderContext.GetStrokePen(2.0);
        var fillBrush = renderContext.GetComponentFillBrush();

        double r = 0.8 * u;
        context.DrawEllipse(fillBrush, strokePen, center, r, r);

        context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(center.X - r, center.Y));
        context.DrawLine(strokePen, new Point(center.X + r, center.Y), new Point(center.X + u, center.Y));

        // Noise jagged symbol
        var noisePen = new Pen(renderContext.IsDark ? Brushes.Orange : Brushes.DarkOrange, 1.6 * z);
        var p0 = new Point(center.X - r * 0.5, center.Y);
        var p1 = new Point(center.X - r * 0.25, center.Y - r * 0.4);
        var p2 = new Point(center.X, center.Y + r * 0.4);
        var p3 = new Point(center.X + r * 0.25, center.Y - r * 0.3);
        var p4 = new Point(center.X + r * 0.5, center.Y);

        context.DrawLine(noisePen, p0, p1);
        context.DrawLine(noisePen, p1, p2);
        context.DrawLine(noisePen, p2, p3);
        context.DrawLine(noisePen, p3, p4);

        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.0 * z, 3.0 * z);
    }
}

public class PulseSourceRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;
        var strokePen = renderContext.GetStrokePen(2.0);
        var fillBrush = renderContext.GetComponentFillBrush();

        double r = 0.8 * u;
        context.DrawEllipse(fillBrush, strokePen, center, r, r);

        context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(center.X - r, center.Y));
        context.DrawLine(strokePen, new Point(center.X + r, center.Y), new Point(center.X + u, center.Y));

        // Draw square wave
        var sqPen = new Pen(renderContext.IsDark ? Brushes.LightGreen : Brushes.Green, 1.8 * z);
        double xL = center.X - r * 0.5;
        double xM1 = center.X - r * 0.2;
        double xM2 = center.X + r * 0.2;
        double xR = center.X + r * 0.5;
        double yHi = center.Y - r * 0.35;
        double yLo = center.Y + r * 0.35;

        context.DrawLine(sqPen, new Point(xL, yLo), new Point(xM1, yLo));
        context.DrawLine(sqPen, new Point(xM1, yLo), new Point(xM1, yHi));
        context.DrawLine(sqPen, new Point(xM1, yHi), new Point(xM2, yHi));
        context.DrawLine(sqPen, new Point(xM2, yHi), new Point(xM2, yLo));
        context.DrawLine(sqPen, new Point(xM2, yLo), new Point(xR, yLo));

        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.0 * z, 3.0 * z);
    }
}
#endregion

#region 2. Math & Signal Renderers (Multiplier, Divider, MathExpr, DeadZone, Relay, RateLimiter)
public class MultiplierRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;
        var strokePen = renderContext.GetStrokePen(2.2);
        var fillBrush = renderContext.GetComponentFillBrush();

        double r = 0.8 * u;
        context.DrawEllipse(fillBrush, strokePen, center, r, r);

        // Terminals: inA (-1, -0.5), inB (-1, +0.5), out (+1, 0), ref (0, 1)
        context.DrawLine(strokePen, new Point(center.X - u, center.Y - 0.5 * u), new Point(center.X - Math.Sqrt(r * r - 0.25 * u * u), center.Y - 0.5 * u));
        context.DrawLine(strokePen, new Point(center.X - u, center.Y + 0.5 * u), new Point(center.X - Math.Sqrt(r * r - 0.25 * u * u), center.Y + 0.5 * u));
        context.DrawLine(strokePen, new Point(center.X + r, center.Y), new Point(center.X + u, center.Y));
        context.DrawLine(strokePen, new Point(center.X, center.Y + r), new Point(center.X, center.Y + u));

        // Multiplication cross symbol ✕
        var crossPen = new Pen(renderContext.IsDark ? Brushes.Cyan : Brushes.DarkBlue, 2.0 * z);
        double s = r * 0.45;
        context.DrawLine(crossPen, new Point(center.X - s, center.Y - s), new Point(center.X + s, center.Y + s));
        context.DrawLine(crossPen, new Point(center.X - s, center.Y + s), new Point(center.X + s, center.Y - s));

        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y - 0.5 * u), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y + 0.5 * u), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y + u), 3.0 * z, 3.0 * z);
    }
}

public class DividerRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;
        var strokePen = renderContext.GetStrokePen(2.2);
        var fillBrush = renderContext.GetComponentFillBrush();

        double r = 0.8 * u;
        context.DrawEllipse(fillBrush, strokePen, center, r, r);

        context.DrawLine(strokePen, new Point(center.X - u, center.Y - 0.5 * u), new Point(center.X - Math.Sqrt(r * r - 0.25 * u * u), center.Y - 0.5 * u));
        context.DrawLine(strokePen, new Point(center.X - u, center.Y + 0.5 * u), new Point(center.X - Math.Sqrt(r * r - 0.25 * u * u), center.Y + 0.5 * u));
        context.DrawLine(strokePen, new Point(center.X + r, center.Y), new Point(center.X + u, center.Y));
        context.DrawLine(strokePen, new Point(center.X, center.Y + r), new Point(center.X, center.Y + u));

        // Division symbol ÷
        var divBrush = renderContext.IsDark ? Brushes.Cyan : Brushes.DarkBlue;
        var divPen = new Pen(divBrush, 2.0 * z);
        double s = r * 0.45;
        context.DrawLine(divPen, new Point(center.X - s, center.Y), new Point(center.X + s, center.Y));
        context.DrawEllipse(divBrush, null, new Point(center.X, center.Y - s * 0.55), 2.5 * z, 2.5 * z);
        context.DrawEllipse(divBrush, null, new Point(center.X, center.Y + s * 0.55), 2.5 * z, 2.5 * z);

        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y - 0.5 * u), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y + 0.5 * u), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y + u), 3.0 * z, 3.0 * z);
    }
}

public class MathExpressionRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;
        var strokePen = renderContext.GetStrokePen(2.2);
        var fillBrush = renderContext.GetComponentFillBrush();

        double boxW = 1.8 * u;
        double boxH = 1.3 * u;
        var rect = new Rect(center.X - boxW / 2.0, center.Y - boxH / 2.0, boxW, boxH);
        context.DrawRectangle(fillBrush, strokePen, rect, 5.0 * z, 5.0 * z);

        context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(rect.Left, center.Y));
        context.DrawLine(strokePen, new Point(rect.Right, center.Y), new Point(center.X + u, center.Y));
        context.DrawLine(strokePen, new Point(center.X, rect.Bottom), new Point(center.X, center.Y + u));

        string expr = (device is MathExpressionDevice me) ? me.Expression : "f(u)";
        if (expr.Length > 10) expr = expr[..8] + "..";

        var textBrush = renderContext.IsDark ? Brushes.LightSkyBlue : Brushes.DarkBlue;
        var text = new FormattedText(
            expr,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.SemiBold),
            9.5 * z,
            textBrush
        );
        context.DrawText(text, new Point(center.X - text.Width / 2.0, center.Y - text.Height / 2.0));

        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y + u), 3.0 * z, 3.0 * z);
    }
}

public class DeadZoneRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;
        var strokePen = renderContext.GetStrokePen(2.2);
        var fillBrush = renderContext.GetComponentFillBrush();

        double boxW = 1.6 * u;
        double boxH = 1.4 * u;
        var rect = new Rect(center.X - boxW / 2.0, center.Y - boxH / 2.0, boxW, boxH);
        context.DrawRectangle(fillBrush, strokePen, rect, 5.0 * z, 5.0 * z);

        context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(rect.Left, center.Y));
        context.DrawLine(strokePen, new Point(rect.Right, center.Y), new Point(center.X + u, center.Y));
        context.DrawLine(strokePen, new Point(center.X, rect.Bottom), new Point(center.X, center.Y + u));

        // Dead-zone curve: /_--/
        var dzPen = new Pen(renderContext.IsDark ? Brushes.Yellow : Brushes.DarkGoldenrod, 1.8 * z);
        double s = boxW * 0.35;
        double dz = boxW * 0.15;
        context.DrawLine(dzPen, new Point(center.X - s, center.Y + s * 0.6), new Point(center.X - dz, center.Y));
        context.DrawLine(dzPen, new Point(center.X - dz, center.Y), new Point(center.X + dz, center.Y));
        context.DrawLine(dzPen, new Point(center.X + dz, center.Y), new Point(center.X + s, center.Y - s * 0.6));

        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y + u), 3.0 * z, 3.0 * z);
    }
}

public class RelayRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;
        var strokePen = renderContext.GetStrokePen(2.2);
        var fillBrush = renderContext.GetComponentFillBrush();

        double boxW = 1.6 * u;
        double boxH = 1.4 * u;
        var rect = new Rect(center.X - boxW / 2.0, center.Y - boxH / 2.0, boxW, boxH);
        context.DrawRectangle(fillBrush, strokePen, rect, 5.0 * z, 5.0 * z);

        context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(rect.Left, center.Y));
        context.DrawLine(strokePen, new Point(rect.Right, center.Y), new Point(center.X + u, center.Y));
        context.DrawLine(strokePen, new Point(center.X, rect.Bottom), new Point(center.X, center.Y + u));

        // Hysteresis loop
        var hPen = new Pen(renderContext.IsDark ? Brushes.Magenta : Brushes.DarkMagenta, 1.8 * z);
        double hw = boxW * 0.25;
        double hh = boxH * 0.28;
        context.DrawLine(hPen, new Point(center.X - hw, center.Y + hh), new Point(center.X + hw, center.Y + hh));
        context.DrawLine(hPen, new Point(center.X + hw, center.Y + hh), new Point(center.X + hw, center.Y - hh));
        context.DrawLine(hPen, new Point(center.X + hw, center.Y - hh), new Point(center.X - hw, center.Y - hh));
        context.DrawLine(hPen, new Point(center.X - hw, center.Y - hh), new Point(center.X - hw, center.Y + hh));

        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y + u), 3.0 * z, 3.0 * z);
    }
}

public class RateLimiterRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;
        var strokePen = renderContext.GetStrokePen(2.2);
        var fillBrush = renderContext.GetComponentFillBrush();

        double boxW = 1.6 * u;
        double boxH = 1.4 * u;
        var rect = new Rect(center.X - boxW / 2.0, center.Y - boxH / 2.0, boxW, boxH);
        context.DrawRectangle(fillBrush, strokePen, rect, 5.0 * z, 5.0 * z);

        context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(rect.Left, center.Y));
        context.DrawLine(strokePen, new Point(rect.Right, center.Y), new Point(center.X + u, center.Y));
        context.DrawLine(strokePen, new Point(center.X, rect.Bottom), new Point(center.X, center.Y + u));

        // Slew slope symbol
        var sPen = new Pen(renderContext.IsDark ? Brushes.LightGreen : Brushes.ForestGreen, 1.8 * z);
        double s = boxW * 0.35;
        context.DrawLine(sPen, new Point(center.X - s, center.Y + s * 0.6), new Point(center.X + s, center.Y - s * 0.6));
        context.DrawLine(strokePen, new Point(center.X - s, center.Y), new Point(center.X + s, center.Y));

        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y + u), 3.0 * z, 3.0 * z);
    }
}
#endregion

#region 3. Dynamic Models (StateSpace, DcMotor, ZOH, UnitDelay, DiscreteTF)
public class StateSpaceBlockRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;
        var strokePen = renderContext.GetStrokePen(2.4);
        var fillBrush = renderContext.GetComponentFillBrush();

        double boxW = 3.2 * u;
        double boxH = 1.8 * u;
        var rect = new Rect(center.X - boxW / 2.0, center.Y - boxH / 2.0, boxW, boxH);
        context.DrawRectangle(fillBrush, strokePen, rect, 6.0 * z, 6.0 * z);

        context.DrawLine(strokePen, new Point(center.X - 2.0 * u, center.Y), new Point(rect.Left, center.Y));
        context.DrawLine(strokePen, new Point(rect.Right, center.Y), new Point(center.X + 2.0 * u, center.Y));
        context.DrawLine(strokePen, new Point(center.X, rect.Bottom), new Point(center.X, center.Y + u));

        var textBrush = renderContext.IsDark ? Brushes.White : Brushes.Black;
        var eq1 = new FormattedText(
            "ẋ = Ax + Bu",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Bold),
            11.0 * z,
            textBrush
        );
        var eq2 = new FormattedText(
            "y = Cx + Du",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Bold),
            11.0 * z,
            textBrush
        );

        context.DrawText(eq1, new Point(center.X - eq1.Width / 2.0, center.Y - eq1.Height - 1.0 * z));
        context.DrawText(eq2, new Point(center.X - eq2.Width / 2.0, center.Y + 2.0 * z));

        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - 2.0 * u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + 2.0 * u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y + u), 3.0 * z, 3.0 * z);
    }
}

public class DcMotorPlantRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;
        var strokePen = renderContext.GetStrokePen(2.4);
        var fillBrush = renderContext.GetComponentFillBrush();

        double boxW = 3.2 * u;
        double boxH = 2.2 * u;
        var rect = new Rect(center.X - boxW / 2.0, center.Y - boxH / 2.0, boxW, boxH);
        context.DrawRectangle(fillBrush, strokePen, rect, 6.0 * z, 6.0 * z);

        // Leads: Arm+ (-2, -0.6), Arm- (-2, 0.6), TL (0, 1.2), omega (2, -0.6), theta (2, 0.6), ref (1, 1.2)
        context.DrawLine(strokePen, new Point(center.X - 2.0 * u, center.Y - 0.6 * u), new Point(rect.Left, center.Y - 0.6 * u));
        context.DrawLine(strokePen, new Point(center.X - 2.0 * u, center.Y + 0.6 * u), new Point(rect.Left, center.Y + 0.6 * u));
        context.DrawLine(strokePen, new Point(rect.Right, center.Y - 0.6 * u), new Point(center.X + 2.0 * u, center.Y - 0.6 * u));
        context.DrawLine(strokePen, new Point(rect.Right, center.Y + 0.6 * u), new Point(center.X + 2.0 * u, center.Y + 0.6 * u));
        context.DrawLine(strokePen, new Point(center.X, rect.Bottom), new Point(center.X, center.Y + 1.2 * u));
        context.DrawLine(strokePen, new Point(center.X + u, rect.Bottom), new Point(center.X + u, center.Y + 1.2 * u));

        // Motor Rotor Circle
        double r = 0.55 * u;
        var rotorBrush = renderContext.IsDark ? new SolidColorBrush(Color.FromArgb(50, 56, 189, 248)) : new SolidColorBrush(Color.FromArgb(30, 2, 132, 199));
        context.DrawEllipse(rotorBrush, strokePen, center, r, r);

        // Shaft & M symbol
        var textBrush = renderContext.IsDark ? Brushes.White : Brushes.Black;
        var mText = new FormattedText(
            "M",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Bold),
            14.0 * z,
            textBrush
        );
        context.DrawText(mText, new Point(center.X - mText.Width / 2.0, center.Y - mText.Height / 2.0));

        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - 2.0 * u, center.Y - 0.6 * u), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X - 2.0 * u, center.Y + 0.6 * u), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + 2.0 * u, center.Y - 0.6 * u), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + 2.0 * u, center.Y + 0.6 * u), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y + 1.2 * u), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y + 1.2 * u), 3.0 * z, 3.0 * z);
    }
}

public class ZeroOrderHoldRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;
        var strokePen = renderContext.GetStrokePen(2.2);
        var fillBrush = renderContext.GetComponentFillBrush();

        double boxW = 1.8 * u;
        double boxH = 1.4 * u;
        var rect = new Rect(center.X - boxW / 2.0, center.Y - boxH / 2.0, boxW, boxH);
        context.DrawRectangle(fillBrush, strokePen, rect, 5.0 * z, 5.0 * z);

        context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(rect.Left, center.Y));
        context.DrawLine(strokePen, new Point(rect.Right, center.Y), new Point(center.X + u, center.Y));
        context.DrawLine(strokePen, new Point(center.X, rect.Bottom), new Point(center.X, center.Y + u));

        // Staircase symbol
        var stairPen = new Pen(renderContext.IsDark ? Brushes.Cyan : Brushes.DarkCyan, 2.0 * z);
        double sx = center.X - boxW * 0.35;
        double sy = center.Y + boxH * 0.25;
        double stepW = boxW * 0.23;
        double stepH = boxH * 0.25;

        context.DrawLine(stairPen, new Point(sx, sy), new Point(sx + stepW, sy));
        context.DrawLine(stairPen, new Point(sx + stepW, sy), new Point(sx + stepW, sy - stepH));
        context.DrawLine(stairPen, new Point(sx + stepW, sy - stepH), new Point(sx + stepW * 2, sy - stepH));
        context.DrawLine(stairPen, new Point(sx + stepW * 2, sy - stepH), new Point(sx + stepW * 2, sy - stepH * 2));
        context.DrawLine(stairPen, new Point(sx + stepW * 2, sy - stepH * 2), new Point(sx + stepW * 3, sy - stepH * 2));

        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y + u), 3.0 * z, 3.0 * z);
    }
}

public class UnitDelayRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;
        var strokePen = renderContext.GetStrokePen(2.2);
        var fillBrush = renderContext.GetComponentFillBrush();

        double boxW = 1.6 * u;
        double boxH = 1.3 * u;
        var rect = new Rect(center.X - boxW / 2.0, center.Y - boxH / 2.0, boxW, boxH);
        context.DrawRectangle(fillBrush, strokePen, rect, 5.0 * z, 5.0 * z);

        context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(rect.Left, center.Y));
        context.DrawLine(strokePen, new Point(rect.Right, center.Y), new Point(center.X + u, center.Y));
        context.DrawLine(strokePen, new Point(center.X, rect.Bottom), new Point(center.X, center.Y + u));

        var textBrush = renderContext.IsDark ? Brushes.LightGreen : Brushes.ForestGreen;
        var text = new FormattedText(
            "z⁻¹",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Bold),
            12.0 * z,
            textBrush
        );
        context.DrawText(text, new Point(center.X - text.Width / 2.0, center.Y - text.Height / 2.0));

        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y + u), 3.0 * z, 3.0 * z);
    }
}

public class DiscreteTransferFunctionRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;
        var strokePen = renderContext.GetStrokePen(2.4);
        var fillBrush = renderContext.GetComponentFillBrush();

        double boxW = 3.0 * u;
        double boxH = 1.6 * u;
        var rect = new Rect(center.X - boxW / 2.0, center.Y - boxH / 2.0, boxW, boxH);
        context.DrawRectangle(fillBrush, strokePen, rect, 6.0 * z, 6.0 * z);

        context.DrawLine(strokePen, new Point(center.X - 2.0 * u, center.Y), new Point(rect.Left, center.Y));
        context.DrawLine(strokePen, new Point(rect.Right, center.Y), new Point(center.X + 2.0 * u, center.Y));
        context.DrawLine(strokePen, new Point(center.X, rect.Bottom), new Point(center.X, center.Y + u));

        string label = (device is DiscreteTransferFunctionDevice dtf)
            ? $"H(z) = {dtf.NumeratorString} / {dtf.DenominatorString}"
            : "H(z)";

        if (label.Length > 16) label = "H(z)";

        var textBrush = renderContext.IsDark ? Brushes.LightGreen : Brushes.ForestGreen;
        var text = new FormattedText(
            label,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Bold),
            11.5 * z,
            textBrush
        );
        context.DrawText(text, new Point(center.X - text.Width / 2.0, center.Y - text.Height / 2.0));

        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - 2.0 * u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + 2.0 * u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y + u), 3.0 * z, 3.0 * z);
    }
}
#endregion
