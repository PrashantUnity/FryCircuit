using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Switch;

public class SwitchRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var strokeBrush = renderContext.IsPlacingGhost
            ? new SolidColorBrush(Color.FromArgb(140, 0, 240, 255))
            : new SolidColorBrush(Color.FromRgb(240, 246, 255));

        var strokePen = new Pen(strokeBrush, 2.8 * z)
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        bool isClosed = (device as SwitchDevice)?.IsClosed ?? false;
        bool isSpdt = device.Type.Equals("spdt", StringComparison.OrdinalIgnoreCase) || device.Pins.Count >= 3;

        if (isSpdt)
        {
            // Terminal pins
            Point p0 = new Point(center.X - u, center.Y);      // Common
            Point p1 = new Point(center.X + u, center.Y - u);  // Throw 1
            Point p2 = new Point(center.X + u, center.Y + u);  // Throw 2

            Point contactCommon = new Point(center.X - u * 0.35, center.Y);
            Point contact1 = new Point(center.X + u * 0.35, center.Y - u * 0.4);
            Point contact2 = new Point(center.X + u * 0.35, center.Y + u * 0.4);

            // Terminal leads
            context.DrawLine(strokePen, p0, contactCommon);
            context.DrawLine(strokePen, contact1, p1);
            context.DrawLine(strokePen, contact2, p2);

            // Contact pads
            var padFill = new SolidColorBrush(Color.FromRgb(20, 28, 40));
            context.DrawEllipse(padFill, strokePen, contactCommon, 3.5 * z, 3.5 * z);
            context.DrawEllipse(padFill, strokePen, contact1, 3.5 * z, 3.5 * z);
            context.DrawEllipse(padFill, strokePen, contact2, 3.5 * z, 3.5 * z);

            // Switch blade (green when connected)
            var bladePen = new Pen(new SolidColorBrush(Color.FromRgb(0, 230, 118)), 3.2 * z) { LineCap = PenLineCap.Round };
            Point targetContact = isClosed ? contact1 : contact2;
            context.DrawLine(bladePen, contactCommon, targetContact);

            // Terminal solder pads
            context.DrawEllipse(strokeBrush, null, p0, 3.2 * z, 3.2 * z);
            context.DrawEllipse(strokeBrush, null, p1, 3.2 * z, 3.2 * z);
            context.DrawEllipse(strokeBrush, null, p2, 3.2 * z, 3.2 * z);
            return;
        }

        // Terminal leads
        Point spstP0 = new Point(center.X, center.Y - u);
        Point spstP1 = new Point(center.X, center.Y + u);
        Point contactTop = new Point(center.X, center.Y - u * 0.4);
        Point contactBottom = new Point(center.X, center.Y + u * 0.4);

        context.DrawLine(strokePen, spstP0, contactTop);
        context.DrawLine(strokePen, contactBottom, spstP1);

        // Contact pads
        var spstPadFill = new SolidColorBrush(Color.FromRgb(20, 28, 40));
        context.DrawEllipse(spstPadFill, strokePen, contactTop, 3.5 * z, 3.5 * z);
        context.DrawEllipse(spstPadFill, strokePen, contactBottom, 3.5 * z, 3.5 * z);

        // Switch blade
        var bladeBrush = isClosed
            ? new SolidColorBrush(Color.FromRgb(0, 230, 118))
            : new SolidColorBrush(Color.FromRgb(255, 170, 0));

        var spstBladePen = new Pen(bladeBrush, 3.2 * z) { LineCap = PenLineCap.Round };

        if (isClosed)
        {
            // Fully bridged between contacts
            context.DrawLine(spstBladePen, contactTop, contactBottom);
        }
        else
        {
            // Open: pivoted at contactTop, swinging open at an angle
            Point openTip = new Point(center.X - u * 0.45, center.Y + u * 0.2);
            context.DrawLine(spstBladePen, contactTop, openTip);
        }

        // Terminal solder pads
        context.DrawEllipse(strokeBrush, null, spstP0, 3.2 * z, 3.2 * z);
        context.DrawEllipse(strokeBrush, null, spstP1, 3.2 * z, 3.2 * z);
    }
}
