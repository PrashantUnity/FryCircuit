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

        var strokeBrush = renderContext.GetStrokeBrush();
        var strokePen = renderContext.GetStrokePen(2.8);

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
            var padFill = renderContext.GetComponentFillBrush() ?? new SolidColorBrush(Color.FromRgb(20, 28, 40));
            context.DrawEllipse(padFill, strokePen, contactCommon, 3.5 * z, 3.5 * z);
            context.DrawEllipse(padFill, strokePen, contact1, 3.5 * z, 3.5 * z);
            context.DrawEllipse(padFill, strokePen, contact2, 3.5 * z, 3.5 * z);

            // Switch blade (green when connected)
            var bladeColor = renderContext.IsDark ? Color.FromRgb(0, 230, 118) : Color.FromRgb(5, 150, 105);
            var bladePen = new Pen(new SolidColorBrush(bladeColor), 3.2 * z) { LineCap = PenLineCap.Round };
            Point targetContact = isClosed ? contact1 : contact2;
            context.DrawLine(bladePen, contactCommon, targetContact);

            // Terminal solder pads
            var padBrush = renderContext.GetSolderPadBrush();
            context.DrawEllipse(padBrush, null, p0, 3.2 * z, 3.2 * z);
            context.DrawEllipse(padBrush, null, p1, 3.2 * z, 3.2 * z);
            context.DrawEllipse(padBrush, null, p2, 3.2 * z, 3.2 * z);
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
        var spstPadFill = renderContext.GetComponentFillBrush() ?? new SolidColorBrush(Color.FromRgb(20, 28, 40));
        context.DrawEllipse(spstPadFill, strokePen, contactTop, 3.5 * z, 3.5 * z);
        context.DrawEllipse(spstPadFill, strokePen, contactBottom, 3.5 * z, 3.5 * z);

        // Switch blade
        var bladeBrush = isClosed
            ? new SolidColorBrush(renderContext.IsDark ? Color.FromRgb(0, 230, 118) : Color.FromRgb(5, 150, 105))
            : new SolidColorBrush(renderContext.IsDark ? Color.FromRgb(255, 170, 0) : Color.FromRgb(217, 119, 6));

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
        var spstPadBrush = renderContext.GetSolderPadBrush();
        context.DrawEllipse(spstPadBrush, null, spstP0, 3.2 * z, 3.2 * z);
        context.DrawEllipse(spstPadBrush, null, spstP1, 3.2 * z, 3.2 * z);
    }
}
