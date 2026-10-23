using System.Globalization;
using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices.Control;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Control;

public class SummingJunctionRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var strokePen = renderContext.GetStrokePen(2.4);
        var fillBrush = renderContext.GetComponentFillBrush();

        double radius = u * 0.55;

        // Circular summing junction body
        context.DrawEllipse(fillBrush, strokePen, center, radius, radius);

        // Terminal leads:
        // Pin 0: Input A at (-1, 0) -> left of circle
        context.DrawLine(strokePen, new Point(center.X - u, center.Y), new Point(center.X - radius, center.Y));

        // Pin 1: Input B at (0, 1) -> bottom of circle
        context.DrawLine(strokePen, new Point(center.X, center.Y + u), new Point(center.X, center.Y + radius));

        // Pin 2: Input C at (0, -1) -> top of circle
        context.DrawLine(strokePen, new Point(center.X, center.Y - u), new Point(center.X, center.Y - radius));

        // Pin 3: Output at (1, 0) -> right of circle
        context.DrawLine(strokePen, new Point(center.X + radius, center.Y), new Point(center.X + u, center.Y));

        // Draw Sigma symbol in center
        var textBrush = renderContext.IsDark
            ? new SolidColorBrush(Color.FromRgb(240, 246, 252))
            : new SolidColorBrush(Color.FromRgb(15, 23, 42));

        var sigmaText = new FormattedText(
            "Σ",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Bold),
            15.0 * z,
            textBrush
        );
        context.DrawText(sigmaText, new Point(center.X - sigmaText.Width / 2.0, center.Y - sigmaText.Height / 2.0));

        // Draw input signs (+, -)
        string signs = "+-";
        if (device is SummingJunctionDevice sumDev)
        {
            signs = sumDev.Signs;
        }

        var signFont = new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Bold);
        double signSize = 10.0 * z;

        // Sign A (Left)
        char sA = signs.Length > 0 ? signs[0] : '+';
        var textA = new FormattedText(sA.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, signFont, signSize, textBrush);
        context.DrawText(textA, new Point(center.X - radius - 11.0 * z, center.Y - 14.0 * z));

        // Sign B (Bottom)
        if (signs.Length > 1)
        {
            char sB = signs[1];
            var textB = new FormattedText(sB.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, signFont, signSize, textBrush);
            context.DrawText(textB, new Point(center.X + 5.0 * z, center.Y + radius - 2.0 * z));
        }

        // Sign C (Top)
        if (signs.Length > 2)
        {
            char sC = signs[2];
            var textC = new FormattedText(sC.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, signFont, signSize, textBrush);
            context.DrawText(textC, new Point(center.X + 5.0 * z, center.Y - radius - 12.0 * z));
        }

        // Solder pads
        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y + u), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y - u), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + u, center.Y), 3.0 * z, 3.0 * z);
    }
}
