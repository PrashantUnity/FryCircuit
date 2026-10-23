using System.Globalization;
using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices.Control;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Control;

public class StepSourceRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var strokePen = renderContext.GetStrokePen(2.4);
        var fillBrush = renderContext.GetComponentFillBrush();

        double radius = u * 0.55;
        context.DrawEllipse(fillBrush, strokePen, center, radius, radius);

        // Terminal leads:
        // Pin 0 (+ Out) at (1, 0)
        context.DrawLine(strokePen, new Point(center.X + radius, center.Y), new Point(center.X + u, center.Y));
        // Pin 1 (- Ref) at (-1, 0)
        context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(center.X - radius, center.Y));

        // Draw Step glyph (low, rise, high)
        var glyphPen = renderContext.GetStrokePen(2.0);
        double gw = radius * 0.65;
        double gh = radius * 0.45;
        var p1 = new Point(center.X - gw, center.Y + gh);
        var p2 = new Point(center.X - gw * 0.2, center.Y + gh);
        var p3 = new Point(center.X - gw * 0.2, center.Y - gh);
        var p4 = new Point(center.X + gw, center.Y - gh);

        context.DrawLine(glyphPen, p1, p2);
        context.DrawLine(glyphPen, p2, p3);
        context.DrawLine(glyphPen, p3, p4);

        // Solder pads
        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y), 3.0 * z, 3.0 * z);
    }
}

public class IntegratorRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var strokePen = renderContext.GetStrokePen(2.4);
        var fillBrush = renderContext.GetComponentFillBrush();

        double boxW = u * 1.5;
        double boxH = u * 1.4;
        var rect = new Rect(center.X - boxW / 2.0, center.Y - boxH / 2.0, boxW, boxH);
        context.DrawRectangle(fillBrush, strokePen, rect, 5.0 * z, 5.0 * z);

        // Leads
        context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(rect.Left, center.Y));
        context.DrawLine(strokePen, new Point(rect.Right, center.Y), new Point(center.X + u, center.Y));
        context.DrawLine(strokePen, new Point(center.X, rect.Bottom), new Point(center.X, center.Y + u));

        // Label: "1/s"
        var textBrush = renderContext.IsDark
            ? new SolidColorBrush(Color.FromRgb(240, 246, 252))
            : new SolidColorBrush(Color.FromRgb(15, 23, 42));

        var txt = new FormattedText("1/s", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Bold), 13.0 * z, textBrush);
        context.DrawText(txt, new Point(center.X - txt.Width / 2.0, center.Y - txt.Height / 2.0));

        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y + u), 3.0 * z, 3.0 * z);
    }
}

public class DerivativeRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var strokePen = renderContext.GetStrokePen(2.4);
        var fillBrush = renderContext.GetComponentFillBrush();

        double boxW = u * 1.5;
        double boxH = u * 1.4;
        var rect = new Rect(center.X - boxW / 2.0, center.Y - boxH / 2.0, boxW, boxH);
        context.DrawRectangle(fillBrush, strokePen, rect, 5.0 * z, 5.0 * z);

        context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(rect.Left, center.Y));
        context.DrawLine(strokePen, new Point(rect.Right, center.Y), new Point(center.X + u, center.Y));
        context.DrawLine(strokePen, new Point(center.X, rect.Bottom), new Point(center.X, center.Y + u));

        var textBrush = renderContext.IsDark
            ? new SolidColorBrush(Color.FromRgb(240, 246, 252))
            : new SolidColorBrush(Color.FromRgb(15, 23, 42));

        var txt = new FormattedText("d/dt", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Bold), 12.0 * z, textBrush);
        context.DrawText(txt, new Point(center.X - txt.Width / 2.0, center.Y - txt.Height / 2.0));

        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y + u), 3.0 * z, 3.0 * z);
    }
}

public class SaturationRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var strokePen = renderContext.GetStrokePen(2.4);
        var fillBrush = renderContext.GetComponentFillBrush();

        double boxW = u * 1.5;
        double boxH = u * 1.4;
        var rect = new Rect(center.X - boxW / 2.0, center.Y - boxH / 2.0, boxW, boxH);
        context.DrawRectangle(fillBrush, strokePen, rect, 5.0 * z, 5.0 * z);

        context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(rect.Left, center.Y));
        context.DrawLine(strokePen, new Point(rect.Right, center.Y), new Point(center.X + u, center.Y));
        context.DrawLine(strokePen, new Point(center.X, rect.Bottom), new Point(center.X, center.Y + u));

        // Draw Saturation Piecewise Curve
        var curvePen = renderContext.GetStrokePen(1.8);
        double hw = boxW * 0.35;
        double hh = boxH * 0.30;
        context.DrawLine(curvePen, new Point(center.X - hw, center.Y + hh), new Point(center.X - hw * 0.4, center.Y + hh));
        context.DrawLine(curvePen, new Point(center.X - hw * 0.4, center.Y + hh), new Point(center.X + hw * 0.4, center.Y - hh));
        context.DrawLine(curvePen, new Point(center.X + hw * 0.4, center.Y - hh), new Point(center.X + hw, center.Y - hh));

        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y + u), 3.0 * z, 3.0 * z);
    }
}

public class TimeDelayRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var strokePen = renderContext.GetStrokePen(2.4);
        var fillBrush = renderContext.GetComponentFillBrush();

        double boxW = u * 1.6;
        double boxH = u * 1.4;
        var rect = new Rect(center.X - boxW / 2.0, center.Y - boxH / 2.0, boxW, boxH);
        context.DrawRectangle(fillBrush, strokePen, rect, 5.0 * z, 5.0 * z);

        context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(rect.Left, center.Y));
        context.DrawLine(strokePen, new Point(rect.Right, center.Y), new Point(center.X + u, center.Y));
        context.DrawLine(strokePen, new Point(center.X, rect.Bottom), new Point(center.X, center.Y + u));

        var textBrush = renderContext.IsDark
            ? new SolidColorBrush(Color.FromRgb(240, 246, 252))
            : new SolidColorBrush(Color.FromRgb(15, 23, 42));

        var txt = new FormattedText("e⁻ˢᵀ", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Bold), 13.0 * z, textBrush);
        context.DrawText(txt, new Point(center.X - txt.Width / 2.0, center.Y - txt.Height / 2.0));

        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y + u), 3.0 * z, 3.0 * z);
    }
}
