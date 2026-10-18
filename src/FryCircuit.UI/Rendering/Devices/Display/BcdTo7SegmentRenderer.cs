using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices.Display;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Display;

public class BcdTo7SegmentRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var bcdDev = device as BcdTo7SegmentDevice;

        var bodyBrush = new SolidColorBrush(Color.FromRgb(22, 28, 38));
        var borderPen = new Pen(new SolidColorBrush(isSelected ? Color.FromArgb(220, 0, 240, 255) : Color.FromRgb(55, 70, 95)), 2.0 * z);
        var pinPen = new Pen(new SolidColorBrush(Color.FromRgb(180, 195, 215)), 2.2 * z);
        var padBrush = new SolidColorBrush(Color.FromRgb(220, 235, 255));

        // IC body dimensions (width: 2.2 * u, height: 3.0 * u)
        double bodyW = u * 2.2;
        double bodyH = u * 3.0;
        var bodyRect = new Rect(center.X - bodyW / 2.0, center.Y - bodyH / 2.0, bodyW, bodyH);

        // Render IC Chip Body with rounded corners
        context.DrawRectangle(bodyBrush, borderPen, bodyRect, 5 * z, 5 * z);

        // Top notch
        var notchGeom = new StreamGeometry();
        using (var g = notchGeom.Open())
        {
            g.BeginFigure(new Point(center.X - u * 0.16, bodyRect.Top), true);
            g.ArcTo(new Point(center.X + u * 0.16, bodyRect.Top), new Size(u * 0.16, u * 0.12), 0, false, SweepDirection.Clockwise);
        }
        context.DrawGeometry(new SolidColorBrush(Color.FromRgb(15, 18, 25)), new Pen(new SolidColorBrush(Color.FromRgb(55, 70, 95)), 1.5 * z), notchGeom);

        // Chip Part Number Text
        var chipLabel = new FormattedText(
            "7448",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            Math.Max(9, 10.5 * z),
            new SolidColorBrush(Color.FromRgb(160, 180, 210))
        );
        context.DrawText(chipLabel, new Point(center.X - chipLabel.Width / 2.0, center.Y - chipLabel.Height / 2.0 - 4 * z));

        // Subtitle
        if (z >= 0.8)
        {
            var subLabel = new FormattedText(
                "BCD→7S",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
                Math.Max(7, 8 * z),
                new SolidColorBrush(Color.FromRgb(100, 130, 165))
            );
            context.DrawText(subLabel, new Point(center.X - subLabel.Width / 2.0, center.Y + chipLabel.Height / 2.0 - 2 * z));
        }

        // Left Inputs: A, B, C, D
        string[] inLabels = { "A", "B", "C", "D" };
        double[] inOffsetsY = { -0.6, -0.2, 0.2, 0.6 };
        for (int i = 0; i < 4; i++)
        {
            var pPin = new Point(center.X - 1.5 * u, center.Y + inOffsetsY[i] * u);
            var pEdge = new Point(bodyRect.Left, center.Y + inOffsetsY[i] * u);

            context.DrawLine(pinPen, pPin, pEdge);
            context.DrawEllipse(padBrush, null, pPin, 3.0 * z, 3.0 * z);

            if (z >= 0.7)
            {
                var txt = new FormattedText(
                    inLabels[i],
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
                    Math.Max(8, 8.5 * z),
                    new SolidColorBrush(Color.FromRgb(140, 165, 195))
                );
                context.DrawText(txt, new Point(bodyRect.Left + 4 * z, pEdge.Y - txt.Height / 2.0));
            }
        }

        // Right Outputs: a, b, c, d, e, f, g
        string[] outLabels = { "a", "b", "c", "d", "e", "f", "g" };
        double[] outOffsetsY = { -1.2, -0.8, -0.4, 0.0, 0.4, 0.8, 1.2 };
        for (int i = 0; i < 7; i++)
        {
            var pPin = new Point(center.X + 1.5 * u, center.Y + outOffsetsY[i] * u);
            var pEdge = new Point(bodyRect.Right, center.Y + outOffsetsY[i] * u);

            context.DrawLine(pinPen, pPin, pEdge);
            context.DrawEllipse(padBrush, null, pPin, 3.0 * z, 3.0 * z);

            if (z >= 0.7)
            {
                var txt = new FormattedText(
                    outLabels[i],
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
                    Math.Max(8, 8.5 * z),
                    new SolidColorBrush(Color.FromRgb(140, 165, 195))
                );
                context.DrawText(txt, new Point(bodyRect.Right - txt.Width - 4 * z, pEdge.Y - txt.Height / 2.0));
            }
        }
    }
}
