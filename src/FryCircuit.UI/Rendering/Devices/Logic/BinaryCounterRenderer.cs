using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices.Logic;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Logic;

public class BinaryCounterRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var counterDev = device as BinaryCounterDevice;
        int val = counterDev?.CounterValue ?? 0;

        var bodyBrush = new SolidColorBrush(Color.FromRgb(22, 28, 38));
        var borderPen = new Pen(new SolidColorBrush(isSelected ? Color.FromArgb(220, 0, 240, 255) : Color.FromRgb(55, 70, 95)), 2.0 * z);
        var pinPen = new Pen(new SolidColorBrush(Color.FromRgb(180, 195, 215)), 2.2 * z);
        var padBrush = new SolidColorBrush(Color.FromRgb(220, 235, 255));

        // IC body dimensions (width: 2.2 * u, height: 3.4 * u)
        double bodyW = u * 2.2;
        double bodyH = u * 3.4;
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
            "74193",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            Math.Max(9, 10.5 * z),
            new SolidColorBrush(Color.FromRgb(160, 180, 210))
        );
        context.DrawText(chipLabel, new Point(center.X - chipLabel.Width / 2.0, center.Y - chipLabel.Height / 2.0 - 12 * z));

        // Subtitle
        if (z >= 0.8)
        {
            var subLabel = new FormattedText(
                "4-BIT CTR",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
                Math.Max(7, 7.5 * z),
                new SolidColorBrush(Color.FromRgb(100, 130, 165))
            );
            context.DrawText(subLabel, new Point(center.X - subLabel.Width / 2.0, center.Y - 2 * z));
        }

        // Live binary state LED indicators on the chip face (Q0, Q1, Q2, Q3)
        double ledSpacing = 9.0 * z;
        double startLedX = center.X - (3 * ledSpacing) / 2.0;
        double ledY = center.Y + 12 * z;
        for (int b = 0; b < 4; b++)
        {
            bool bitOn = ((val >> b) & 1) != 0;
            var ledColor = bitOn ? Color.FromRgb(0, 240, 120) : Color.FromRgb(30, 45, 55);
            var ledPt = new Point(startLedX + b * ledSpacing, ledY);
            if (bitOn)
            {
                context.DrawEllipse(new SolidColorBrush(Color.FromArgb(80, 0, 240, 120)), null, ledPt, 4.5 * z, 4.5 * z);
            }
            context.DrawEllipse(new SolidColorBrush(ledColor), null, ledPt, 2.8 * z, 2.8 * z);
        }

        // Left Pins: UP (-0.9), DOWN (-0.3), CLR (0.9), BW (1.5)
        (string lbl, double yOff)[] leftPins = {
            ("UP", -0.9),
            ("DN", -0.3),
            ("CLR", 0.9),
            ("BW", 1.5)
        };
        foreach (var (lbl, yOff) in leftPins)
        {
            var pPin = new Point(center.X - 1.5 * u, center.Y + yOff * u);
            var pEdge = new Point(bodyRect.Left, center.Y + yOff * u);

            context.DrawLine(pinPen, pPin, pEdge);
            context.DrawEllipse(padBrush, null, pPin, 3.0 * z, 3.0 * z);

            if (z >= 0.7)
            {
                var txt = new FormattedText(
                    lbl,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
                    Math.Max(7, 8 * z),
                    new SolidColorBrush(Color.FromRgb(140, 165, 195))
                );
                context.DrawText(txt, new Point(bodyRect.Left + 4 * z, pEdge.Y - txt.Height / 2.0));
            }
        }

        // Right Pins: 1 (-0.9), 2 (-0.3), 4 (0.3), 8 (0.9), CY (1.5)
        (string lbl, double yOff)[] rightPins = {
            ("1", -0.9),
            ("2", -0.3),
            ("4", 0.3),
            ("8", 0.9),
            ("CY", 1.5)
        };
        foreach (var (lbl, yOff) in rightPins)
        {
            var pPin = new Point(center.X + 1.5 * u, center.Y + yOff * u);
            var pEdge = new Point(bodyRect.Right, center.Y + yOff * u);

            context.DrawLine(pinPen, pPin, pEdge);
            context.DrawEllipse(padBrush, null, pPin, 3.0 * z, 3.0 * z);

            if (z >= 0.7)
            {
                var txt = new FormattedText(
                    lbl,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
                    Math.Max(7, 8 * z),
                    new SolidColorBrush(Color.FromRgb(140, 165, 195))
                );
                context.DrawText(txt, new Point(bodyRect.Right - txt.Width - 4 * z, pEdge.Y - txt.Height / 2.0));
            }
        }
    }
}
