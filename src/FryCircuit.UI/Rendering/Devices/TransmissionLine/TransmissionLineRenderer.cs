using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.TransmissionLine;

/// <summary>
/// Professional RF / Microwave Transmission Line renderer for aerospace and drone telemetry tracks.
/// </summary>
public class TransmissionLineRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var tDev = device as TransmissionLineDevice;

        var strokeBrush = renderContext.IsPlacingGhost
            ? new SolidColorBrush(Color.FromArgb(140, 0, 240, 255))
            : new SolidColorBrush(Color.FromRgb(240, 246, 255));

        var strokePen = new Pen(strokeBrush, 2.5 * z)
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        var bodyBrush = new SolidColorBrush(Color.FromArgb(200, 15, 20, 30));
        var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 230, 118)), 1.8 * z);
        var wavePen = new Pen(new SolidColorBrush(Color.FromArgb(160, 0, 240, 255)), 1.5 * z);

        double w = u * 3.2;
        double h = u * 1.8;
        var boxRect = new Rect(center.X - w / 2.0, center.Y - h / 2.0, w, h);

        // Render shielded transmission line body
        context.DrawRectangle(bodyBrush, borderPen, boxRect, 4 * z, 4 * z);

        // Terminal leads matching pin coordinates:
        // Pin 0: In+ (-2, -1.0)
        // Pin 1: In- (-2, 1.0)
        // Pin 2: Out+ (2, -1.0)
        // Pin 3: Out- (2, 1.0)
        double pInX = center.X - 2.0 * u;
        double pOutX = center.X + 2.0 * u;
        double pTopY = center.Y - 1.0 * u;
        double pBotY = center.Y + 1.0 * u;

        context.DrawLine(strokePen, new Point(pInX, pTopY), new Point(boxRect.Left, pTopY));
        context.DrawLine(strokePen, new Point(pInX, pBotY), new Point(boxRect.Left, pBotY));
        context.DrawLine(strokePen, new Point(boxRect.Right, pTopY), new Point(pOutX, pTopY));
        context.DrawLine(strokePen, new Point(boxRect.Right, pBotY), new Point(pOutX, pBotY));

        // Terminal pads
        context.DrawEllipse(strokeBrush, null, new Point(pInX, pTopY), 3.2 * z, 3.2 * z);
        context.DrawEllipse(strokeBrush, null, new Point(pInX, pBotY), 3.2 * z, 3.2 * z);
        context.DrawEllipse(strokeBrush, null, new Point(pOutX, pTopY), 3.2 * z, 3.2 * z);
        context.DrawEllipse(strokeBrush, null, new Point(pOutX, pBotY), 3.2 * z, 3.2 * z);

        // Internal coaxial / microstrip wave graphic (spiral / wave lines)
        context.DrawLine(wavePen, new Point(boxRect.Left + 6 * z, pTopY), new Point(boxRect.Right - 6 * z, pTopY));
        context.DrawLine(wavePen, new Point(boxRect.Left + 6 * z, pBotY), new Point(boxRect.Right - 6 * z, pBotY));

        // Diagonal ground shield cross-hatching
        for (double x = boxRect.Left + 10 * z; x < boxRect.Right - 5 * z; x += 12 * z)
        {
            context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(50, 0, 230, 118)), 1.2 * z),
                new Point(x, pTopY + 4 * z), new Point(x + 6 * z, pBotY - 4 * z));
        }

        // Transmission line schematic label: Title and RF Parameters
        var ftTitle = new FormattedText(
            "T-LINE",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            Math.Max(8, 9.5 * z),
            new SolidColorBrush(Color.FromRgb(0, 240, 255))
        );
        context.DrawText(ftTitle, new Point(center.X - ftTitle.Width / 2.0, center.Y - ftTitle.Height - 1 * z));

        string paramText = $"{FryCircuit.Core.Devices.Base.DeviceDescriptorBase.FormatEngineering(tDev?.CharacteristicImpedance ?? 50.0, "Ω")} · {FryCircuit.Core.Devices.Base.DeviceDescriptorBase.FormatEngineering(tDev?.DelaySeconds ?? 250e-12, "s")}";
        var ftParams = new FormattedText(
            paramText,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
            Math.Max(7, 8.0 * z),
            new SolidColorBrush(Color.FromRgb(220, 235, 255))
        );
        context.DrawText(ftParams, new Point(center.X - ftParams.Width / 2.0, center.Y + 1 * z));
    }
}
