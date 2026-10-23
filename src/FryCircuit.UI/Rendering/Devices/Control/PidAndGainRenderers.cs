using System.Globalization;
using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices.Control;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Control;

public class PidControllerRenderer : IDeviceRenderer
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

        // Body rectangle
        context.DrawRectangle(fillBrush, strokePen, rect, 5.0 * z, 5.0 * z);

        // Leads:
        // Input at (-1, 0)
        context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(rect.Left, center.Y));
        // Output at (1, 0)
        context.DrawLine(strokePen, new Point(rect.Right, center.Y), new Point(center.X + u, center.Y));
        // Ref at (0, 1)
        context.DrawLine(strokePen, new Point(center.X, rect.Bottom), new Point(center.X, center.Y + u));

        // Draw "PID" text in center
        var textBrush = renderContext.IsDark
            ? new SolidColorBrush(Color.FromRgb(240, 246, 252))
            : new SolidColorBrush(Color.FromRgb(15, 23, 42));

        var pidText = new FormattedText(
            "PID",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Bold),
            12.0 * z,
            textBrush
        );
        context.DrawText(pidText, new Point(center.X - pidText.Width / 2.0, center.Y - pidText.Height / 2.0));

        // Solder pads
        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y + u), 3.0 * z, 3.0 * z);
    }
}

public class GainBlockRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var strokePen = renderContext.GetStrokePen(2.4);
        var fillBrush = renderContext.GetComponentFillBrush();

        // Triangular forward block pointing right (+X)
        double triLeft = center.X - u * 0.55;
        double triRight = center.X + u * 0.55;
        double triTop = center.Y - u * 0.65;
        double triBottom = center.Y + u * 0.65;

        var geom = new StreamGeometry();
        using (var g = geom.Open())
        {
            g.BeginFigure(new Point(triLeft, triTop), true);
            g.LineTo(new Point(triLeft, triBottom));
            g.LineTo(new Point(triRight, center.Y));
        }
        context.DrawGeometry(fillBrush, strokePen, geom);

        // Leads:
        // Input at (-1, 0)
        context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(triLeft, center.Y));
        // Output at (1, 0)
        context.DrawLine(strokePen, new Point(triRight, center.Y), new Point(center.X + u, center.Y));

        // Gain label: e.g. "x10" or "K"
        string label = "K";
        if (device is GainBlockDevice gb)
        {
            label = $"×{gb.Gain:G3}";
        }

        var textBrush = renderContext.IsDark
            ? new SolidColorBrush(Color.FromRgb(240, 246, 252))
            : new SolidColorBrush(Color.FromRgb(15, 23, 42));

        var gainText = new FormattedText(
            label,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.SemiBold),
            9.5 * z,
            textBrush
        );
        context.DrawText(gainText, new Point(center.X - u * 0.18 - gainText.Width / 2.0, center.Y - gainText.Height / 2.0));

        // Solder pads
        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.0 * z, 3.0 * z);
    }
}
