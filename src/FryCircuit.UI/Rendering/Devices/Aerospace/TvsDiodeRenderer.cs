using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices.Aerospace;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Aerospace;

/// <summary>
/// Industrial Vector CAD renderer for Transient Voltage Suppressor (TVS) Clamping Diodes.
/// Renders standard semiconductor TVS symbol with dual bent cathode bar hooks,
/// active surge clamping glow effect, and breakdown voltage telemetry.
/// </summary>
public class TvsDiodeRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double size, bool isSelected, DeviceRenderContext renderContext)
    {
        var tvs = device as TvsDiodeDevice;
        double u = size;
        double cx = center.X;
        double cy = center.Y;
        double z = renderContext.Zoom;

        // Terminal leads: Anode (-u, 0), Cathode (u, 0)
        Point pAnode = new(cx - u, cy);
        Point pCathode = new(cx + u, cy);

        double triW = u * 0.42;
        double triH = u * 0.42;

        var strokeBrush = renderContext.GetStrokeBrush();
        var strokePen = renderContext.GetStrokePen(2.2);

        // Terminal wires to symbol
        context.DrawLine(strokePen, pAnode, new Point(cx - triW, cy));
        context.DrawLine(strokePen, new Point(cx + triW, cy), pCathode);

        // Active Surge Clamping Aura Glow
        if (tvs != null && tvs.IsClamping)
        {
            var glowBrush = new RadialGradientBrush
            {
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(180, 244, 63, 94), 0.0), // Crimson surge glow
                    new GradientStop(Color.FromArgb(0, 244, 63, 94), 1.0)
                }
            };
            context.DrawEllipse(glowBrush, null, center, u * 0.85, u * 0.85);
        }

        // Diode Triangular Body pointing from Anode to Cathode
        var triGeom = new StreamGeometry();
        using (var gctx = triGeom.Open())
        {
            gctx.BeginFigure(new Point(cx - triW, cy - triH), isFilled: true);
            gctx.LineTo(new Point(cx + triW, cy));
            gctx.LineTo(new Point(cx - triW, cy + triH));
            gctx.EndFigure(isClosed: true);
        }
        var bodyBrush = renderContext.GetComponentFillBrush() ?? new SolidColorBrush(Color.FromArgb(200, 30, 41, 59));
        context.DrawGeometry(bodyBrush, strokePen, triGeom);

        // TVS Cathode Bar with dual bent Zener hooks (Top bent right, Bottom bent left)
        double hookSize = triH * 0.4;
        var barPen = new Pen(strokeBrush, 2.4 * z);
        context.DrawLine(barPen, new Point(cx + triW, cy - triH), new Point(cx + triW, cy + triH));
        // Top hook (bent right)
        context.DrawLine(barPen, new Point(cx + triW, cy - triH), new Point(cx + triW + hookSize, cy - triH));
        // Bottom hook (bent left)
        context.DrawLine(barPen, new Point(cx + triW, cy + triH), new Point(cx + triW - hookSize, cy + triH));

        // Telemetry Badge
        string text = tvs != null
            ? (tvs.IsClamping ? $"CLAMPING ({tvs.VoltageDrop:F1}V)" : $"{tvs.BreakdownVoltage:F1}V TVS")
            : "TVS Diode";

        var textColor = (tvs != null && tvs.IsClamping)
            ? Color.FromRgb(244, 63, 94)
            : (renderContext.IsDark ? Color.FromRgb(148, 163, 184) : Color.FromRgb(71, 85, 105));
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            9 * z,
            new SolidColorBrush(textColor)
        );
        context.DrawText(ft, new Point(cx - ft.Width / 2.0, cy - triH - 13 * z));

        // Solder pads
        var padBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(padBrush, null, pAnode, 3.0 * z, 3.0 * z);
        context.DrawEllipse(padBrush, null, pCathode, 3.0 * z, 3.0 * z);
    }
}
