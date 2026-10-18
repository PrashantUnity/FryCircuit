using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Sensors;

/// <summary>
/// Professional Drone & Robotics CAD renderer for 4-pin Hall Effect Magnetic Sensor.
/// Displays semiconductor sensor body, differential outputs, and magnetic field vector indicator.
/// </summary>
public class HallEffectSensorRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double size, bool isSelected, DeviceRenderContext renderContext)
    {
        var hall = device as HallEffectSensorDevice;
        var bodyBrush = new SolidColorBrush(Color.FromRgb(16, 22, 32));
        var bodyPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 230, 160)), 1.8 * renderContext.Zoom);
        var pinPen = new Pen(new SolidColorBrush(Color.FromRgb(160, 190, 220)), 1.8 * renderContext.Zoom);

        double bw = size * 1.3;
        double bh = size * 2.5;
        var boxRect = new Rect(center.X - bw / 2.0, center.Y - bh / 2.0, bw, bh);

        // IC Package Body
        context.DrawRectangle(bodyBrush, bodyPen, boxRect, 4, 4);

        // Terminal pin leads (Orthogonal horizontal lines connecting to pins at Y = ±size)
        double topPinY = center.Y - size;
        double bottomPinY = center.Y + size;

        // Pin 0: Vcc (Top Left)
        context.DrawLine(pinPen, new Point(center.X - size, topPinY), new Point(boxRect.Left, topPinY));
        // Pin 1: GND (Bottom Left)
        context.DrawLine(pinPen, new Point(center.X - size, bottomPinY), new Point(boxRect.Left, bottomPinY));
        // Pin 2: Out+ (Top Right)
        context.DrawLine(pinPen, new Point(boxRect.Right, topPinY), new Point(center.X + size, topPinY));
        // Pin 3: Out- (Bottom Right)
        context.DrawLine(pinPen, new Point(boxRect.Right, bottomPinY), new Point(center.X + size, bottomPinY));

        // Semiconductor Hall Plate Rhombus in center
        var platePen = new Pen(new SolidColorBrush(Color.FromArgb(180, 0, 240, 255)), 1.4 * renderContext.Zoom);
        var plateGeom = new StreamGeometry();
        double rw = bw * 0.38;
        double rh = size * 0.55;
        using (var ctx = plateGeom.Open())
        {
            ctx.BeginFigure(new Point(center.X, center.Y - rh), isFilled: false);
            ctx.LineTo(new Point(center.X + rw, center.Y));
            ctx.LineTo(new Point(center.X, center.Y + rh));
            ctx.LineTo(new Point(center.X - rw, center.Y));
            ctx.EndFigure(isClosed: true);
        }
        context.DrawGeometry(null, platePen, plateGeom);

        // "HALL" text label in center
        var ft = new FormattedText(
            "HALL",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            Math.Max(8, 9.5 * renderContext.Zoom),
            new SolidColorBrush(Color.FromRgb(0, 230, 160))
        );
        context.DrawText(ft, new Point(center.X - ft.Width / 2.0, center.Y - ft.Height / 2.0));

        // Solder pins
        var dotBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        context.DrawEllipse(dotBrush, null, new Point(center.X - size, topPinY), 2.2 * renderContext.Zoom, 2.2 * renderContext.Zoom);
        context.DrawEllipse(dotBrush, null, new Point(center.X - size, bottomPinY), 2.2 * renderContext.Zoom, 2.2 * renderContext.Zoom);
        context.DrawEllipse(dotBrush, null, new Point(center.X + size, topPinY), 2.2 * renderContext.Zoom, 2.2 * renderContext.Zoom);
        context.DrawEllipse(dotBrush, null, new Point(center.X + size, bottomPinY), 2.2 * renderContext.Zoom, 2.2 * renderContext.Zoom);
    }
}
