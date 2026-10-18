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
            return new SolidColorBrush(Color.FromArgb(140, 0, 240, 255));
        }
        if (IsGlowPass)
        {
            var glowColor = IsDragCollision
                ? Color.FromArgb(190, 255, 60, 60)
                : Color.FromArgb(180, 0, 240, 255);
            return new SolidColorBrush(glowColor);
        }
        if (IsSelected)
        {
            var selColor = IsDragCollision
                ? Color.FromRgb(255, 75, 75)
                : Color.FromRgb(0, 240, 255);
            return new SolidColorBrush(selColor);
        }
        return new SolidColorBrush(defaultColor ?? Color.FromRgb(240, 246, 255));
    }

    public Pen GetStrokePen(double baseThickness = 2.8, Color? defaultColor = null)
    {
        if (IsPlacingGhost)
        {
            return new Pen(new SolidColorBrush(Color.FromArgb(140, 0, 240, 255)), baseThickness * Zoom)
            {
                LineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
        }
        if (IsGlowPass)
        {
            var glowColor = IsDragCollision
                ? Color.FromArgb(190, 255, 60, 60)
                : Color.FromArgb(180, 0, 240, 255);
            return new Pen(new SolidColorBrush(glowColor), (baseThickness + 5.5) * Zoom)
            {
                LineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
        }
        if (IsSelected)
        {
            var selColor = IsDragCollision
                ? Color.FromRgb(255, 75, 75)
                : Color.FromRgb(0, 240, 255);
            return new Pen(new SolidColorBrush(selColor), (baseThickness + 0.4) * Zoom)
            {
                LineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
        }
        return new Pen(new SolidColorBrush(defaultColor ?? Color.FromRgb(240, 246, 255)), baseThickness * Zoom)
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
