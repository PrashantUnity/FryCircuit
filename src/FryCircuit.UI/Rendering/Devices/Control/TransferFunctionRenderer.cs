using System.Globalization;
using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices.Control;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Control;

public class TransferFunctionRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var strokePen = renderContext.GetStrokePen(2.4);
        var fillBrush = renderContext.GetComponentFillBrush();

        // Rectangular body: width = 3.2 * u, height = 1.8 * u
        double boxW = u * 3.2;
        double boxH = u * 1.8;
        var rect = new Rect(center.X - boxW / 2.0, center.Y - boxH / 2.0, boxW, boxH);

        // Draw background box with rounded corners
        context.DrawRectangle(fillBrush, strokePen, rect, 6.0 * z, 6.0 * z);

        // Leads:
        // Pin 0 (Input) at (-2, 0) relative to center -> connects to left of box
        context.DrawLine(strokePen, new Point(center.X - 2.0 * u, center.Y), new Point(rect.Left, center.Y));

        // Pin 1 (Output) at (+2, 0) relative to center -> connects to right of box
        context.DrawLine(strokePen, new Point(rect.Right, center.Y), new Point(center.X + 2.0 * u, center.Y));

        // Pin 2 (Ref) at (0, 1) relative to center -> connects to bottom of box
        context.DrawLine(strokePen, new Point(center.X, rect.Bottom), new Point(center.X, center.Y + u));

        // Format Transfer Function text: Num / Den
        string num = "1";
        string den = "s + 1";
        if (device is TransferFunctionDevice tf)
        {
            num = tf.CurrentTransferFunction.Numerator.ToAcademicString("s");
            den = tf.CurrentTransferFunction.Denominator.ToAcademicString("s");
        }

        // Draw fraction bar
        var textBrush = renderContext.IsDark
            ? new SolidColorBrush(Color.FromRgb(240, 246, 252))
            : new SolidColorBrush(Color.FromRgb(15, 23, 42));

        var barPen = new Pen(textBrush, 1.8 * z);
        double barY = center.Y;
        double barHalfW = boxW * 0.40;
        context.DrawLine(barPen, new Point(center.X - barHalfW, barY), new Point(center.X + barHalfW, barY));

        // Draw Numerator (top)
        var numText = new FormattedText(
            num,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.SemiBold),
            11.5 * z,
            textBrush
        );
        context.DrawText(numText, new Point(center.X - numText.Width / 2.0, barY - numText.Height - 2.0 * z));

        // Draw Denominator (bottom)
        var denText = new FormattedText(
            den,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.SemiBold),
            11.5 * z,
            textBrush
        );
        context.DrawText(denText, new Point(center.X - denText.Width / 2.0, barY + 3.0 * z));

        // Solder pads
        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X - 2.0 * u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X + 2.0 * u, center.Y), 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y + u), 3.0 * z, 3.0 * z);
    }
}
