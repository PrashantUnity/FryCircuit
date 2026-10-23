using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices.Battery;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Battery;

/// <summary>
/// Professional IEEE Vector CAD renderer for Aerospace & Drone Electrochemical Battery Cells.
/// Renders alternating long anode and thick cathode plates, terminal polarity markers,
/// and dynamic State of Charge (SOC) level indicator.
/// </summary>
public class BatteryCellRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double size, bool isSelected, DeviceRenderContext renderContext)
    {
        var battery = device as BatteryCellDevice;
        double u = size;
        double cx = center.X;
        double cy = center.Y;

        var leadPen = renderContext.GetLeadPen(2.2);

        // Terminal leads: Left Pin 0 (+), Right Pin 1 (-)
        Point pLeft = new(cx - u, cy);
        Point pRight = new(cx + u, cy);
        Point pLongPlate = new(cx - u * 0.25, cy);
        Point pShortPlate = new(cx + u * 0.25, cy);

        // Wire leads
        context.DrawLine(leadPen, pLeft, pLongPlate);
        context.DrawLine(leadPen, pRight, pShortPlate);

        // Positive Plate: Long thin plate (Anode / Cathode in standard notation)
        var posColor = renderContext.IsDark ? Color.FromRgb(0, 230, 118) : Color.FromRgb(5, 150, 105); // Emerald Green
        var posPen = new Pen(new SolidColorBrush(posColor), 2.5 * renderContext.Zoom);
        context.DrawLine(posPen, new Point(pLongPlate.X, cy - u * 0.75), new Point(pLongPlate.X, cy + u * 0.75));

        // Negative Plate: Short thick plate
        var negColor = renderContext.IsDark ? Color.FromRgb(255, 171, 0) : Color.FromRgb(217, 119, 6); // Amber
        var negPen = new Pen(new SolidColorBrush(negColor), 4.5 * renderContext.Zoom);
        context.DrawLine(negPen, new Point(pShortPlate.X, cy - u * 0.45), new Point(pShortPlate.X, cy + u * 0.45));

        // Polarity Markers
        var ftPlus = new FormattedText(
            "+",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.ExtraBold),
            12 * renderContext.Zoom,
            new SolidColorBrush(posColor)
        );
        context.DrawText(ftPlus, new Point(cx - u * 0.75, cy - u * 0.85));

        var ftMinus = new FormattedText(
            "-",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.ExtraBold),
            14 * renderContext.Zoom,
            new SolidColorBrush(negColor)
        );
        context.DrawText(ftMinus, new Point(cx + u * 0.55, cy - u * 0.85));

        // State of Charge (SOC) Bar & Value
        if (battery != null)
        {
            double soc = Math.Clamp(battery.StateOfCharge, 0.0, 1.0);
            string socText = $"{soc * 100:F0}% ({battery.OpenCircuitVoltage:F2}V)";
            var socColor = soc > 0.4
                ? (renderContext.IsDark ? Color.FromRgb(0, 230, 118) : Color.FromRgb(5, 150, 105))
                : (soc > 0.15
                    ? (renderContext.IsDark ? Color.FromRgb(255, 171, 0) : Color.FromRgb(217, 119, 6))
                    : (renderContext.IsDark ? Color.FromRgb(255, 61, 0) : Color.FromRgb(220, 38, 38)));

            var ftSoc = new FormattedText(
                socText,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
                9 * renderContext.Zoom,
                new SolidColorBrush(socColor)
            );
            context.DrawText(ftSoc, new Point(cx - ftSoc.Width / 2.0, cy + u * 0.65));
        }

        // Terminal solder pads
        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, pLeft, 3.2 * renderContext.Zoom, 3.2 * renderContext.Zoom);
        context.DrawEllipse(padBrush, null, pRight, 3.2 * renderContext.Zoom, 3.2 * renderContext.Zoom);
    }
}
