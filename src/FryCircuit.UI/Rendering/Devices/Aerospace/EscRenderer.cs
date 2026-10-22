using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices.Aerospace;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Aerospace;

/// <summary>
/// Professional Drone Electronic Speed Controller (ESC) Inverter CAD renderer.
/// Renders industrial matte-black PCB with gold immersion pads, onboard GaN MOSFET switches,
/// low-ESR bulk electrolytic capacitor, and throttle status badges.
/// </summary>
public class EscRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double size, bool isSelected, DeviceRenderContext renderContext)
    {
        var esc = device as EscDevice;
        double u = size;
        double cx = center.X;
        double cy = center.Y;
        double z = renderContext.Zoom;

        double w = u * 3.4;
        double h = u * 2.2;
        var pcbRect = new Rect(cx - w / 2.0, cy - h / 2.0, w, h);

        var strokePen = renderContext.GetStrokePen(1.8, Color.FromRgb(217, 119, 6)); // Gold PCB border
        var pcbBrush = new SolidColorBrush(Color.FromArgb(245, 15, 23, 42)); // Dark Slate

        // PCB Body
        context.DrawRectangle(pcbBrush, strokePen, pcbRect, 4.0 * z, 4.0 * z);

        // 4 Gold Corner Mounting Holes
        double holeOffX = w * 0.42;
        double holeOffY = h * 0.38;
        var goldPen = new Pen(new SolidColorBrush(Color.FromRgb(245, 158, 11)), 1.2 * z);
        context.DrawEllipse(null, goldPen, new Point(cx - holeOffX, cy - holeOffY), 2.0 * z, 2.0 * z);
        context.DrawEllipse(null, goldPen, new Point(cx + holeOffX, cy - holeOffY), 2.0 * z, 2.0 * z);
        context.DrawEllipse(null, goldPen, new Point(cx - holeOffX, cy + holeOffY), 2.0 * z, 2.0 * z);
        context.DrawEllipse(null, goldPen, new Point(cx + holeOffX, cy + holeOffY), 2.0 * z, 2.0 * z);

        // Bulk Low-ESR Capacitor Footprint on PCB
        var capBrush = new SolidColorBrush(Color.FromArgb(200, 30, 41, 59));
        var capPen = new Pen(new SolidColorBrush(Color.FromRgb(100, 116, 139)), 1.2 * z);
        context.DrawEllipse(capBrush, capPen, new Point(cx - u * 0.4, cy), u * 0.35, u * 0.35);

        // 6 MOSFET Bridge IC Packages (3 High-Side at col 1, 3 Low-Side at col 2)
        var mosfetBrush = new SolidColorBrush(Color.FromRgb(40, 50, 70));
        var mosfetPen = new Pen(new SolidColorBrush(Color.FromRgb(148, 163, 184)), 0.8 * z);
        double mosW = u * 0.24;
        double mosH = u * 0.32;
        for (int i = -1; i <= 1; i++)
        {
            // High-Side FET
            context.DrawRectangle(mosfetBrush, mosfetPen, new Rect(cx + u * 0.20, cy + i * u * 0.48 - mosH / 2.0, mosW, mosH), 1.0 * z, 1.0 * z);
            // Low-Side FET
            context.DrawRectangle(mosfetBrush, mosfetPen, new Rect(cx + u * 0.55, cy + i * u * 0.48 - mosH / 2.0, mosW, mosH), 1.0 * z, 1.0 * z);
        }

        // Terminal Pins & Labels
        // Left Pins: Pin 0 (VBAT+, -2u, -u), Pin 1 (GND, -2u, u), Pin 2 (PWM, -2u, 0)
        Point pVbat = new(cx - 2.0 * u, cy - u);
        Point pGnd = new(cx - 2.0 * u, cy + u);
        Point pPwm = new(cx - 2.0 * u, cy);

        // Right Pins: Pin 3 (U, 2u, -u), Pin 4 (V, 2u, 0), Pin 5 (W, 2u, u)
        Point pU = new(cx + 2.0 * u, cy - u);
        Point pV = new(cx + 2.0 * u, cy);
        Point pW = new(cx + 2.0 * u, cy + u);

        var leadPen = new Pen(renderContext.GetStrokeBrush(Color.FromRgb(200, 215, 235)), 2.0 * z);
        context.DrawLine(leadPen, pVbat, new Point(cx - w / 2.0, cy - u));
        context.DrawLine(leadPen, pGnd, new Point(cx - w / 2.0, cy + u));
        context.DrawLine(leadPen, pPwm, new Point(cx - w / 2.0, cy));

        context.DrawLine(leadPen, pU, new Point(cx + w / 2.0, cy - u));
        context.DrawLine(leadPen, pV, new Point(cx + w / 2.0, cy));
        context.DrawLine(leadPen, pW, new Point(cx + w / 2.0, cy + u));

        // Terminal Pads
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(239, 68, 68)), null, pVbat, 3.2 * z, 3.2 * z); // VBAT+ Red
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(148, 163, 184)), null, pGnd, 3.2 * z, 3.2 * z); // GND Slate
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(245, 158, 11)), null, pPwm, 2.8 * z, 2.8 * z);  // PWM Amber

        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(244, 63, 94)), null, pU, 3.2 * z, 3.2 * z);  // Phase U
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(250, 204, 21)), null, pV, 3.2 * z, 3.2 * z); // Phase V
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(56, 189, 248)), null, pW, 3.2 * z, 3.2 * z); // Phase W
    }
}
