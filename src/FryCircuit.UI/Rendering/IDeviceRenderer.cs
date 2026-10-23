using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering;

public class DeviceRenderContext
{
    public double Zoom { get; set; } = 1.0;
    public double GridSize { get; set; } = 50.0;
    public bool IsPlacingGhost { get; set; } = false;
    public bool IsSelected { get; set; } = false;
    public bool IsDragCollision { get; set; } = false;
    public bool IsGlowPass { get; set; } = false;

    public IBrush GetStrokeBrush(Color? defaultColor = null)
    {
        if (IsPlacingGhost)
        {
            var ghostColor = IsDragCollision
                ? Color.FromArgb(160, 244, 63, 94)
                : Color.FromArgb(140, 56, 189, 248);
            return new SolidColorBrush(ghostColor);
        }
        if (IsGlowPass)
        {
            var glowColor = IsDragCollision
                ? Color.FromArgb(160, 244, 63, 94)
                : Color.FromArgb(100, 56, 189, 248);
            return new SolidColorBrush(glowColor);
        }
        if (IsSelected)
        {
            var selColor = IsDragCollision
                ? Color.FromRgb(244, 63, 94)
                : Color.FromRgb(56, 189, 248);
            return new SolidColorBrush(selColor);
        }
        return new SolidColorBrush(defaultColor ?? Color.FromRgb(226, 232, 240));
    }

    public Pen GetStrokePen(double baseThickness = 2.4, Color? defaultColor = null)
    {
        if (IsPlacingGhost)
        {
            var ghostColor = IsDragCollision
                ? Color.FromArgb(180, 244, 63, 94)
                : Color.FromArgb(140, 56, 189, 248);
            return new Pen(new SolidColorBrush(ghostColor), baseThickness * Zoom)
            {
                LineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
        }
        if (IsGlowPass)
        {
            var glowColor = IsDragCollision
                ? Color.FromArgb(160, 244, 63, 94)
                : Color.FromArgb(100, 56, 189, 248);
            return new Pen(new SolidColorBrush(glowColor), (baseThickness + 4.5) * Zoom)
            {
                LineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
        }
        if (IsSelected)
        {
            var selColor = IsDragCollision
                ? Color.FromRgb(244, 63, 94)
                : Color.FromRgb(56, 189, 248);
            return new Pen(new SolidColorBrush(selColor), (baseThickness + 0.3) * Zoom)
            {
                LineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
        }
        return new Pen(new SolidColorBrush(defaultColor ?? Color.FromRgb(226, 232, 240)), baseThickness * Zoom)
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
    }
}

public interface IDeviceRenderer
{
    void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext);
}
