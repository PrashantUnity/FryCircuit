using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Timer;

public class Timer555Renderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var timerDev = device as Timer555Device;
        bool isHigh = timerDev?.LatchState == true;

        var bodyBrush = new SolidColorBrush(Color.FromRgb(22, 28, 38));
        var borderPen = new Pen(new SolidColorBrush(isSelected ? Color.FromArgb(220, 0, 240, 255) : Color.FromRgb(55, 70, 95)), 2.0 * z);
        var pinPen = new Pen(new SolidColorBrush(Color.FromRgb(180, 195, 215)), 2.5 * z)
        {
            LineCap = PenLineCap.Round
        };
        var padBrush = new SolidColorBrush(Color.FromRgb(220, 235, 255));

        // IC DIP-8 body dimensions (width: 2.2 * u, height: 2.4 * u)
        double bodyW = u * 2.2;
        double bodyH = u * 2.4;
        var bodyRect = new Rect(center.X - bodyW / 2.0, center.Y - bodyH / 2.0, bodyW, bodyH);

        // Render IC Chip Body with rounded corners
        context.DrawRectangle(bodyBrush, borderPen, bodyRect, 6 * z, 6 * z);

        // Chip orientation notch at the top
        var notchBrush = new SolidColorBrush(Color.FromRgb(15, 18, 25));
        var notchPen = new Pen(new SolidColorBrush(Color.FromRgb(55, 70, 95)), 1.5 * z);
        var notchGeom = new StreamGeometry();
        using (var g = notchGeom.Open())
        {
            g.BeginFigure(new Point(center.X - u * 0.18, bodyRect.Top), true);
            g.ArcTo(new Point(center.X + u * 0.18, bodyRect.Top), new Size(u * 0.18, u * 0.14), 0, false, SweepDirection.Clockwise);
        }
        context.DrawGeometry(notchBrush, notchPen, notchGeom);

        // Chip Part Number Text
        var chipLabel = new FormattedText(
            "NE555",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            Math.Max(10, 11 * z),
            new SolidColorBrush(Color.FromRgb(160, 180, 210))
        );
        context.DrawText(chipLabel, new Point(center.X - chipLabel.Width / 2.0, center.Y - chipLabel.Height / 2.0 - 2 * z));

        // Output Status Indicator Glow Dot
        var ledColor = isHigh ? Color.FromArgb(220, 0, 230, 118) : Color.FromArgb(80, 255, 70, 70);
        context.DrawEllipse(new SolidColorBrush(ledColor), null, new Point(center.X, center.Y + u * 0.6), 4.5 * z, 4.5 * z);

        // Pins & Labels
        // Pin layout matches Timer555Device.Pins:
        // 0: GND (bottom) (0, 1.5)
        // 1: TRIG (left-bottom) (-1.5, 0.8)
        // 2: OUT (right) (1.5, 0.0)
        // 3: RESET (left-top) (-1.5, -1.0)
        // 4: CTRL (right-bottom) (1.5, 1.0)
        // 5: THRESH (left-middle) (-1.5, 0.2)
        // 6: DISCH (left-upper) (-1.5, -0.4)
        // 7: VCC (top) (0, -1.5)

        DrawPin(context, center, new Point(0, 1.5), new Point(0, 1.2), "GND", u, z, pinPen, padBrush);
        DrawPin(context, center, new Point(-1.5, 0.8), new Point(-1.1, 0.8), "TRG", u, z, pinPen, padBrush);
        DrawPin(context, center, new Point(1.5, 0.0), new Point(1.1, 0.0), "OUT", u, z, pinPen, padBrush);
        DrawPin(context, center, new Point(-1.5, -1.0), new Point(-1.1, -1.0), "RST", u, z, pinPen, padBrush);
        DrawPin(context, center, new Point(1.5, 1.0), new Point(1.1, 1.0), "CV", u, z, pinPen, padBrush);
        DrawPin(context, center, new Point(-1.5, 0.2), new Point(-1.1, 0.2), "THR", u, z, pinPen, padBrush);
        DrawPin(context, center, new Point(-1.5, -0.4), new Point(-1.1, -0.4), "DIS", u, z, pinPen, padBrush);
        DrawPin(context, center, new Point(0, -1.5), new Point(0, -1.2), "VCC", u, z, pinPen, padBrush);
    }

    private static void DrawPin(DrawingContext context, Point center, Point pinOffset, Point bodyEdgeOffset, string label, double u, double z, Pen pinPen, IBrush padBrush)
    {
        var pPin = new Point(center.X + pinOffset.X * u, center.Y + pinOffset.Y * u);
        var pEdge = new Point(center.X + bodyEdgeOffset.X * u, center.Y + bodyEdgeOffset.Y * u);

        // Draw pin lead
        context.DrawLine(pinPen, pPin, pEdge);

        // Terminal pad dot
        context.DrawEllipse(padBrush, null, pPin, 3.2 * z, 3.2 * z);

        // Pin label text inside body near lead
        if (z >= 0.7)
        {
            var txt = new FormattedText(
                label,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
                Math.Max(8, 8.5 * z),
                new SolidColorBrush(Color.FromRgb(120, 145, 175))
            );

            // Compute label position inside IC border
            Point txtPos;
            if (pinOffset.X < 0)
                txtPos = new Point(pEdge.X + 3 * z, pEdge.Y - txt.Height / 2.0);
            else if (pinOffset.X > 0)
                txtPos = new Point(pEdge.X - txt.Width - 3 * z, pEdge.Y - txt.Height / 2.0);
            else if (pinOffset.Y < 0)
                txtPos = new Point(pEdge.X - txt.Width / 2.0, pEdge.Y + 2 * z);
            else
                txtPos = new Point(pEdge.X - txt.Width / 2.0, pEdge.Y - txt.Height - 2 * z);

            context.DrawText(txt, txtPos);
        }
    }
}
