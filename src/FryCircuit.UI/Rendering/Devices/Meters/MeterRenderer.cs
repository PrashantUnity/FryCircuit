using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Meters;

public class MeterRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var strokeBrush = renderContext.GetStrokeBrush();
        var strokePen = renderContext.GetStrokePen(2.8);

        var fillBrush = renderContext.GetComponentFillBrush();

        // Leads from pins to circle
        double radius = u * 0.55;
        context.DrawLine(strokePen, new Point(center.X, center.Y - u), new Point(center.X, center.Y - radius));
        context.DrawLine(strokePen, new Point(center.X, center.Y + radius), new Point(center.X, center.Y + u));

        // Meter circle
        context.DrawEllipse(fillBrush, strokePen, center, radius, radius);

        bool isVolt = device.Type.Contains("volt", StringComparison.OrdinalIgnoreCase);
        bool isOhm = device.Type.Contains("ohm", StringComparison.OrdinalIgnoreCase);
        string symbol = isVolt ? "V" : (isOhm ? "Ω" : "A");
        Color symColor = isVolt
            ? (renderContext.IsDark ? Color.FromRgb(0, 240, 255) : Color.FromRgb(2, 132, 199))
            : (isOhm
                ? (renderContext.IsDark ? Color.FromRgb(255, 179, 0) : Color.FromRgb(217, 119, 6))
                : (renderContext.IsDark ? Color.FromRgb(0, 230, 118) : Color.FromRgb(5, 150, 105)));

        var text = new FormattedText(
            symbol,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            Math.Max(12, 16 * z),
            new SolidColorBrush(symColor)
        );
        double rotAngle = (int)device.Transform.Rotation;
        Point textCenter = new Point(center.X, center.Y);
        Point textOrigin = new Point(center.X - text.Width / 2.0, center.Y - text.Height / 2.0);

        if (rotAngle != 0)
        {
            using (context.PushTransform(Matrix.CreateTranslation(-textCenter.X, -textCenter.Y) *
                                         Matrix.CreateRotation(-rotAngle * Math.PI / 180.0) *
                                         Matrix.CreateTranslation(textCenter.X, textCenter.Y)))
            {
                context.DrawText(text, textOrigin);
            }
        }
        else
        {
            context.DrawText(text, textOrigin);
        }

        // Terminal solder pads
        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y - u), 3.2 * z, 3.2 * z);
        context.DrawEllipse(padBrush, null, new Point(center.X, center.Y + u), 3.2 * z, 3.2 * z);

        // Polarity indicators (+ for Pin 0 Up, - for Pin 1 Down) - omit for non-polar ohmmeters
        if (!isOhm)
        {
            var polarBrush = renderContext.GetLeadBrush();
            var plusText = new FormattedText(
                "+",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
                Math.Max(9, 10 * z),
                polarBrush
            );
            Point plusPt = new Point(center.X + 6 * z, center.Y - radius - 14 * z);
            if (rotAngle != 0)
            {
                Point pC = new Point(plusPt.X + plusText.Width / 2.0, plusPt.Y + plusText.Height / 2.0);
                using (context.PushTransform(Matrix.CreateTranslation(-pC.X, -pC.Y) *
                                             Matrix.CreateRotation(-rotAngle * Math.PI / 180.0) *
                                             Matrix.CreateTranslation(pC.X, pC.Y)))
                {
                    context.DrawText(plusText, plusPt);
                }
            }
            else
            {
                context.DrawText(plusText, plusPt);
            }

            var minusText = new FormattedText(
                "−",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
                Math.Max(9, 10 * z),
                polarBrush
            );
            Point minusPt = new Point(center.X + 6 * z, center.Y + radius + 2 * z);
            if (rotAngle != 0)
            {
                Point mC = new Point(minusPt.X + minusText.Width / 2.0, minusPt.Y + minusText.Height / 2.0);
                using (context.PushTransform(Matrix.CreateTranslation(-mC.X, -mC.Y) *
                                             Matrix.CreateRotation(-rotAngle * Math.PI / 180.0) *
                                             Matrix.CreateTranslation(mC.X, mC.Y)))
                {
                    context.DrawText(minusText, minusPt);
                }
            }
            else
            {
                context.DrawText(minusText, minusPt);
            }
        }
    }
}
