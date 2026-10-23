using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.NetLabel;

/// <summary>
/// Professional EDA Net Label / Global Rail Marker Renderer.
/// Draws an EDA standard flag badge with the net name and pin attachment.
/// </summary>
public class NetLabelRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double size, bool isSelected, DeviceRenderContext renderContext)
    {
        var net = device as NetLabelDevice;
        string label = net?.NetName ?? "NET";

        var textColor = renderContext.IsDark ? Color.FromRgb(0, 240, 255) : Color.FromRgb(2, 132, 199);
        var ft = new FormattedText(
            label,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.ExtraBold),
            Math.Max(9, 10.5 * renderContext.Zoom),
            new SolidColorBrush(textColor)
        );

        double padX = 6.0 * renderContext.Zoom;
        double padY = 3.0 * renderContext.Zoom;
        double bw = ft.Width + padX * 2 + 8.0 * renderContext.Zoom;
        double bh = ft.Height + padY * 2;

        var flagGeom = new StreamGeometry();
        using (var ctx = flagGeom.Open())
        {
            // Pointed pentagon flag pointing left toward the connection pin (center)
            ctx.BeginFigure(new Point(center.X, center.Y), isFilled: true);
            ctx.LineTo(new Point(center.X + 8.0 * renderContext.Zoom, center.Y - bh / 2.0));
            ctx.LineTo(new Point(center.X + bw, center.Y - bh / 2.0));
            ctx.LineTo(new Point(center.X + bw, center.Y + bh / 2.0));
            ctx.LineTo(new Point(center.X + 8.0 * renderContext.Zoom, center.Y + bh / 2.0));
            ctx.EndFigure(isClosed: true);
        }

        var fillBrush = renderContext.IsDark
            ? new SolidColorBrush(Color.FromArgb(220, 14, 24, 38))
            : new SolidColorBrush(Color.FromArgb(240, 240, 249, 255));
        var borderPen = new Pen(new SolidColorBrush(textColor), 1.5 * renderContext.Zoom);

        context.DrawGeometry(fillBrush, borderPen, flagGeom);
        context.DrawText(ft, new Point(center.X + 9.0 * renderContext.Zoom + padX, center.Y - ft.Height / 2.0));

        // Attachment pin solder dot
        var dotBrush = new SolidColorBrush(textColor);
        context.DrawEllipse(dotBrush, null, center, 3.0 * renderContext.Zoom, 3.0 * renderContext.Zoom);
    }
}
