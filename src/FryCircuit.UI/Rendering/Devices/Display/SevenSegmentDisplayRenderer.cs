using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Devices.Display;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering.Devices.Display;

/// <summary>
/// High-fidelity 7-Segment LED numerical and hexadecimal display renderer.
/// FryCircuit illuminated emission halos and authentic beveled segment geometries.
/// </summary>
public class SevenSegmentDisplayRenderer : IDeviceRenderer
{
    public void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext)
    {
        double u = unitSize;
        double z = renderContext.Zoom;

        var segDev = device as SevenSegmentDisplayDevice;

        // Display module dimensions
        double modW = u * 2.2;
        double modH = u * 3.0;
        var modRect = new Rect(center.X - modW / 2.0, center.Y - modH / 2.0, modW, modH);

        // 1. Draw outer module enclosure (bezel)
        var bezelBrush = renderContext.IsDark ? new SolidColorBrush(Color.FromRgb(12, 16, 22)) : new SolidColorBrush(Color.FromRgb(248, 250, 252));
        var bezelPen = renderContext.GetStrokePen(2.0,
            isSelected ? Color.FromArgb(220, 0, 240, 255) : Color.FromRgb(45, 60, 80),
            isSelected ? Color.FromArgb(220, 0, 136, 204) : Color.FromRgb(203, 213, 225));
        context.DrawRectangle(bezelBrush, bezelPen, modRect, 6 * z, 6 * z);

        // Subtle inner display face inset
        var faceRect = new Rect(modRect.Left + 4 * z, modRect.Top + 4 * z, modRect.Width - 8 * z, modRect.Height - 8 * z);
        var faceBrush = renderContext.IsDark ? new SolidColorBrush(Color.FromRgb(8, 12, 16)) : new SolidColorBrush(Color.FromRgb(241, 245, 249));
        var faceBorderPen = new Pen(new SolidColorBrush(renderContext.IsDark ? Color.FromArgb(50, 25, 35, 48) : Color.FromArgb(60, 203, 213, 225)), 1 * z);
        context.DrawRectangle(faceBrush, faceBorderPen, faceRect, 4 * z, 4 * z);

        // 2. LED Color palette (Emerald Green by default)
        Color litColor = renderContext.IsDark ? Color.FromRgb(38, 255, 78) : Color.FromRgb(5, 150, 105);      // Radiant emerald lime
        Color haloGlow = renderContext.IsDark ? Color.FromArgb(120, 0, 255, 100) : Color.FromArgb(80, 5, 150, 105);
        Color outerHalo = renderContext.IsDark ? Color.FromArgb(40, 0, 255, 100) : Color.FromArgb(30, 5, 150, 105);
        Color unlitColor = renderContext.IsDark ? Color.FromRgb(15, 32, 22) : Color.FromArgb(35, 100, 116, 139); // Visible silhouette

        if (segDev?.LedColor == "red")
        {
            litColor = renderContext.IsDark ? Color.FromRgb(255, 48, 48) : Color.FromRgb(220, 38, 38);
            haloGlow = renderContext.IsDark ? Color.FromArgb(120, 255, 48, 48) : Color.FromArgb(80, 220, 38, 38);
            outerHalo = renderContext.IsDark ? Color.FromArgb(40, 255, 48, 48) : Color.FromArgb(30, 220, 38, 38);
            unlitColor = renderContext.IsDark ? Color.FromRgb(32, 15, 18) : Color.FromArgb(35, 100, 116, 139);
        }
        else if (segDev?.LedColor == "amber")
        {
            litColor = renderContext.IsDark ? Color.FromRgb(255, 180, 20) : Color.FromRgb(217, 119, 6);
            haloGlow = renderContext.IsDark ? Color.FromArgb(120, 255, 180, 20) : Color.FromArgb(80, 217, 119, 6);
            outerHalo = renderContext.IsDark ? Color.FromArgb(40, 255, 180, 20) : Color.FromArgb(30, 217, 119, 6);
            unlitColor = renderContext.IsDark ? Color.FromRgb(32, 26, 12) : Color.FromArgb(35, 100, 116, 139);
        }

        // 3. Render 7 Segments (a, b, c, d, e, f, g) + DP
        // Geometry offsets centered on the face
        double segL = u * 0.72; // segment length
        double segT = u * 0.15; // segment thickness
        double cx = center.X + u * 0.1; // slight offset to leave room for left pin labels
        double cy = center.Y;

        // Slant angle (italic tilt)
        double slant = 0.06;

        for (int i = 0; i < 7; i++)
        {
            bool isLit = segDev?.SegmentState[i] ?? false;
            double brightness = segDev?.SegmentBrightness[i] ?? (isLit ? 1.0 : 0.0);

            var segGeom = CreateSegmentGeometry(i, cx, cy, segL, segT, slant);

            if (brightness > 0.05)
            {
                // Multi-stage radiant halo
                var outerPen = new Pen(new SolidColorBrush(outerHalo), (segT + 12.0) * z)
                {
                    LineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };
                var midPen = new Pen(new SolidColorBrush(haloGlow), (segT + 5.0) * z)
                {
                    LineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };
                var coreBrush = new SolidColorBrush(litColor);

                context.DrawGeometry(null, outerPen, segGeom);
                context.DrawGeometry(null, midPen, segGeom);
                context.DrawGeometry(coreBrush, null, segGeom);
            }
            else
            {
                context.DrawGeometry(new SolidColorBrush(unlitColor), null, segGeom);
            }
        }

        // Decimal Point (DP)
        Point dpCenter = new Point(cx + segL * 0.65, cy + segL * 0.95);
        double dpR = segT * 0.6;
        bool isDpLit = segDev?.DpState ?? false;
        if (isDpLit)
        {
            context.DrawEllipse(new SolidColorBrush(outerHalo), null, dpCenter, dpR * 2.2 * z, dpR * 2.2 * z);
            context.DrawEllipse(new SolidColorBrush(haloGlow), null, dpCenter, dpR * 1.5 * z, dpR * 1.5 * z);
            context.DrawEllipse(new SolidColorBrush(litColor), null, dpCenter, dpR * z, dpR * z);
        }
        else
        {
            context.DrawEllipse(new SolidColorBrush(unlitColor), null, dpCenter, dpR * z, dpR * z);
        }

        // 4. Draw Leads and Labels on Left Pins (a..g) and Bottom Pin (COM)
        var pinPen = renderContext.GetLeadPen(2.2);
        var padBrush = renderContext.GetSolderPadBrush();

        string[] segPinNames = { "a", "b", "c", "d", "e", "f", "g" };
        double[] segPinY = { -1.2, -0.8, -0.4, 0.0, 0.4, 0.8, 1.2 };
        var pinTextColor = renderContext.IsDark ? Color.FromRgb(140, 165, 195) : Color.FromRgb(71, 85, 105);

        for (int p = 0; p < 7; p++)
        {
            var pPin = new Point(center.X - 1.5 * u, center.Y + segPinY[p] * u);
            var pEdge = new Point(modRect.Left, center.Y + segPinY[p] * u);

            context.DrawLine(pinPen, pPin, pEdge);
            context.DrawEllipse(padBrush, null, pPin, 3.0 * z, 3.0 * z);

            if (z >= 0.7)
            {
                var txt = new FormattedText(
                    segPinNames[p],
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
                    Math.Max(8, 8.5 * z),
                    new SolidColorBrush(pinTextColor)
                );
                context.DrawText(txt, new Point(modRect.Left + 3 * z, pEdge.Y - txt.Height / 2.0));
            }
        }

        // COM Lead at bottom
        var pComPin = new Point(center.X, center.Y + 1.8 * u);
        var pComEdge = new Point(center.X, modRect.Bottom);
        context.DrawLine(pinPen, pComPin, pComEdge);
        context.DrawEllipse(padBrush, null, pComPin, 3.0 * z, 3.0 * z);

        if (z >= 0.7)
        {
            var txtCom = new FormattedText(
                "COM",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
                Math.Max(7, 8 * z),
                new SolidColorBrush(pinTextColor)
            );
            context.DrawText(txtCom, new Point(center.X - txtCom.Width / 2.0, modRect.Bottom - txtCom.Height - 2 * z));
        }

        // DP Lead at right bottom
        var pDpPin = new Point(center.X + 1.5 * u, center.Y + 1.2 * u);
        var pDpEdge = new Point(modRect.Right, center.Y + 1.2 * u);
        context.DrawLine(pinPen, pDpPin, pDpEdge);
        context.DrawEllipse(padBrush, null, pDpPin, 3.0 * z, 3.0 * z);
    }

    private static StreamGeometry CreateSegmentGeometry(int segIndex, double cx, double cy, double l, double t, double slant)
    {
        var geom = new StreamGeometry();
        double halfL = l / 2.0;
        double halfT = t / 2.0;

        using (var g = geom.Open())
        {
            switch (segIndex)
            {
                case 0: // a: top horizontal
                {
                    double y = cy - l;
                    double x = cx - y * slant;
                    g.BeginFigure(new Point(x - halfL, y), true);
                    g.LineTo(new Point(x - halfL + halfT, y - halfT));
                    g.LineTo(new Point(x + halfL - halfT, y - halfT));
                    g.LineTo(new Point(x + halfL, y));
                    g.LineTo(new Point(x + halfL - halfT, y + halfT));
                    g.LineTo(new Point(x - halfL + halfT, y + halfT));
                    break;
                }
                case 1: // b: top-right vertical
                {
                    double y1 = cy - l;
                    double y2 = cy;
                    double x1 = cx + halfL - y1 * slant;
                    double x2 = cx + halfL - y2 * slant;
                    g.BeginFigure(new Point(x1, y1 + halfT), true);
                    g.LineTo(new Point(x1 + halfT, y1 + halfT + halfT));
                    g.LineTo(new Point(x2 + halfT, y2 - halfT - halfT));
                    g.LineTo(new Point(x2, y2 - halfT));
                    g.LineTo(new Point(x2 - halfT, y2 - halfT - halfT));
                    g.LineTo(new Point(x1 - halfT, y1 + halfT + halfT));
                    break;
                }
                case 2: // c: bottom-right vertical
                {
                    double y1 = cy;
                    double y2 = cy + l;
                    double x1 = cx + halfL - y1 * slant;
                    double x2 = cx + halfL - y2 * slant;
                    g.BeginFigure(new Point(x1, y1 + halfT), true);
                    g.LineTo(new Point(x1 + halfT, y1 + halfT + halfT));
                    g.LineTo(new Point(x2 + halfT, y2 - halfT - halfT));
                    g.LineTo(new Point(x2, y2 - halfT));
                    g.LineTo(new Point(x2 - halfT, y2 - halfT - halfT));
                    g.LineTo(new Point(x1 - halfT, y1 + halfT + halfT));
                    break;
                }
                case 3: // d: bottom horizontal
                {
                    double y = cy + l;
                    double x = cx - y * slant;
                    g.BeginFigure(new Point(x - halfL, y), true);
                    g.LineTo(new Point(x - halfL + halfT, y - halfT));
                    g.LineTo(new Point(x + halfL - halfT, y - halfT));
                    g.LineTo(new Point(x + halfL, y));
                    g.LineTo(new Point(x + halfL - halfT, y + halfT));
                    g.LineTo(new Point(x - halfL + halfT, y + halfT));
                    break;
                }
                case 4: // e: bottom-left vertical
                {
                    double y1 = cy;
                    double y2 = cy + l;
                    double x1 = cx - halfL - y1 * slant;
                    double x2 = cx - halfL - y2 * slant;
                    g.BeginFigure(new Point(x1, y1 + halfT), true);
                    g.LineTo(new Point(x1 + halfT, y1 + halfT + halfT));
                    g.LineTo(new Point(x2 + halfT, y2 - halfT - halfT));
                    g.LineTo(new Point(x2, y2 - halfT));
                    g.LineTo(new Point(x2 - halfT, y2 - halfT - halfT));
                    g.LineTo(new Point(x1 - halfT, y1 + halfT + halfT));
                    break;
                }
                case 5: // f: top-left vertical
                {
                    double y1 = cy - l;
                    double y2 = cy;
                    double x1 = cx - halfL - y1 * slant;
                    double x2 = cx - halfL - y2 * slant;
                    g.BeginFigure(new Point(x1, y1 + halfT), true);
                    g.LineTo(new Point(x1 + halfT, y1 + halfT + halfT));
                    g.LineTo(new Point(x2 + halfT, y2 - halfT - halfT));
                    g.LineTo(new Point(x2, y2 - halfT));
                    g.LineTo(new Point(x2 - halfT, y2 - halfT - halfT));
                    g.LineTo(new Point(x1 - halfT, y1 + halfT + halfT));
                    break;
                }
                case 6: // g: middle horizontal
                default:
                {
                    double y = cy;
                    double x = cx - y * slant;
                    g.BeginFigure(new Point(x - halfL, y), true);
                    g.LineTo(new Point(x - halfL + halfT, y - halfT));
                    g.LineTo(new Point(x + halfL - halfT, y - halfT));
                    g.LineTo(new Point(x + halfL, y));
                    g.LineTo(new Point(x + halfL - halfT, y + halfT));
                    g.LineTo(new Point(x - halfL + halfT, y + halfT));
                    break;
                }
            }
        }

        return geom;
    }
}
