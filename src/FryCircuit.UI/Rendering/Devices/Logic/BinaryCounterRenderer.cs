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

        var bodyBrush = renderContext.GetComponentFillBrush() ?? new SolidColorBrush(Color.FromRgb(22, 28, 38));
        var borderPen = renderContext.GetStrokePen(2.0,
            isSelected ? Color.FromArgb(220, 0, 240, 255) : Color.FromRgb(55, 70, 95),
            isSelected ? Color.FromArgb(220, 0, 136, 204) : Color.FromRgb(203, 213, 225));
        var pinPen = renderContext.GetLeadPen(2.2);
        var padBrush = renderContext.GetSolderPadBrush();

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
        var notchBrush = renderContext.IsDark ? new SolidColorBrush(Color.FromRgb(15, 18, 25)) : new SolidColorBrush(Color.FromRgb(226, 232, 240));
        var notchPen = new Pen(new SolidColorBrush(renderContext.IsDark ? Color.FromRgb(55, 70, 95) : Color.FromRgb(203, 213, 225)), 1.5 * z);
        context.DrawGeometry(notchBrush, notchPen, notchGeom);

        // Chip Part Number Text
        var chipLabel = new FormattedText(
            "74193",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            Math.Max(9, 10.5 * z),
            new SolidColorBrush(renderContext.IsDark ? Color.FromRgb(160, 180, 210) : Color.FromRgb(15, 23, 42))
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
                new SolidColorBrush(renderContext.IsDark ? Color.FromRgb(100, 130, 165) : Color.FromRgb(71, 85, 105))
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
            var ledColor = bitOn
                ? (renderContext.IsDark ? Color.FromRgb(0, 240, 120) : Color.FromRgb(5, 150, 105))
                : (renderContext.IsDark ? Color.FromRgb(30, 45, 55) : Color.FromRgb(226, 232, 240));
            var ledPt = new Point(startLedX + b * ledSpacing, ledY);
            if (bitOn)
            {
                var glowColor = renderContext.IsDark ? Color.FromArgb(80, 0, 240, 120) : Color.FromArgb(80, 5, 150, 105);
                context.DrawEllipse(new SolidColorBrush(glowColor), null, ledPt, 4.5 * z, 4.5 * z);
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
        var pinLabelColor = renderContext.IsDark ? Color.FromRgb(140, 165, 195) : Color.FromRgb(71, 85, 105);
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
                    new SolidColorBrush(pinLabelColor)
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
                    new SolidColorBrush(pinLabelColor)
                );
                context.DrawText(txt, new Point(bodyRect.Right - txt.Width - 4 * z, pEdge.Y - txt.Height / 2.0));
            }
        }
    }
}
