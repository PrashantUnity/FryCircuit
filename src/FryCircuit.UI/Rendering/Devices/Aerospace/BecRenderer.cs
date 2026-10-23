using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices.Aerospace;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Aerospace;

/// <summary>
/// Industrial Vector CAD renderer for Drone Battery Eliminator Circuit (BEC) / Step-Down DC-DC Regulators.
/// Displays switching power IC package, buck inductor coil emblem, and live regulated rail status.
/// </summary>
public class BecRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double size, bool isSelected, DeviceRenderContext renderContext)
    {
        var bec = device as BecDevice;
        double u = size;
        double cx = center.X;
        double cy = center.Y;
        double z = renderContext.Zoom;

        double boxW = u * 1.4;
        double boxH = u * 2.3;
        var boxRect = new Rect(cx - boxW / 2.0, cy - boxH / 2.0, boxW, boxH);

        var boxBrush = renderContext.GetComponentFillBrush() ?? new SolidColorBrush(Color.FromArgb(240, 15, 23, 42));
        var boxPen = renderContext.GetStrokePen(2.0, Color.FromRgb(16, 185, 129), Color.FromRgb(5, 150, 105)); // Emerald regulator border
        context.DrawRectangle(boxBrush, boxPen, boxRect, 4.0 * z, 4.0 * z);

        // Terminal Leads: Pin 0 (VIN, -u, -u), Pin 1 (GND, -u, u), Pin 2 (VOUT, u, 0)
        Point pVin = new(cx - u, cy - u);
        Point pGnd = new(cx - u, cy + u);
        Point pVout = new(cx + u, cy);

        var leadPen = renderContext.GetLeadPen(2.0);
        context.DrawLine(leadPen, pVin, new Point(cx - boxW / 2.0, cy - u));
        context.DrawLine(leadPen, pGnd, new Point(cx - boxW / 2.0, cy + u));
        context.DrawLine(leadPen, pVout, new Point(cx + boxW / 2.0, cy));

        // Center Step-Down Buck Symbol (DC-DC Converter arrow / inductor)
        var symbolColor = renderContext.IsDark ? Color.FromRgb(16, 185, 129) : Color.FromRgb(5, 150, 105);
        var symbolPen = new Pen(new SolidColorBrush(symbolColor), 2.0 * z);
        context.DrawLine(symbolPen, new Point(cx - boxW * 0.28, cy - boxH * 0.2), new Point(cx + boxW * 0.28, cy + boxH * 0.2));

        // Step-down indicator arrow
        var arrowGeom = new StreamGeometry();
        using (var gctx = arrowGeom.Open())
        {
            gctx.BeginFigure(new Point(cx + boxW * 0.28, cy + boxH * 0.2), isFilled: true);
            gctx.LineTo(new Point(cx + boxW * 0.10, cy + boxH * 0.2));
            gctx.LineTo(new Point(cx + boxW * 0.28, cy + boxH * 0.02));
            gctx.EndFigure(isClosed: true);
        }
        context.DrawGeometry(new SolidColorBrush(symbolColor), null, arrowGeom);

        // Micro Inductor Core graphic in top quadrant
        var indPen = new Pen(new SolidColorBrush(renderContext.IsDark ? Color.FromArgb(160, 52, 211, 153) : Color.FromArgb(160, 5, 150, 105)), 1.2 * z);
        context.DrawEllipse(null, indPen, new Point(cx, cy - boxH * 0.25), boxW * 0.22, boxW * 0.22);

        // Solder pads
        context.DrawEllipse(new SolidColorBrush(renderContext.IsDark ? Color.FromRgb(239, 68, 68) : Color.FromRgb(220, 38, 38)), null, pVin, 3.2 * z, 3.2 * z);
        context.DrawEllipse(new SolidColorBrush(renderContext.IsDark ? Color.FromRgb(148, 163, 184) : Color.FromRgb(71, 85, 105)), null, pGnd, 3.2 * z, 3.2 * z);
        context.DrawEllipse(new SolidColorBrush(renderContext.IsDark ? Color.FromRgb(16, 185, 129) : Color.FromRgb(5, 150, 105)), null, pVout, 3.2 * z, 3.2 * z);
    }
}
