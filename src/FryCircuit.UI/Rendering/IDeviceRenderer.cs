using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Models;
using FryCircuit.UI.Theming;

namespace FryCircuit.UI.Rendering;

public class DeviceRenderContext
{
    public double Zoom { get; set; } = 1.0;
    public double GridSize { get; set; } = 50.0;
    public bool IsPlacingGhost { get; set; } = false;
    public bool IsSelected { get; set; } = false;
    public bool IsDragCollision { get; set; } = false;
    public bool IsGlowPass { get; set; } = false;

    public CircuitThemeTokens? Tokens { get; set; }
    public bool IsDark => Tokens?.IsDark ?? CircuitThemeService.Instance.IsDark;

    public IBrush GetStrokeBrush(Color? darkDefault = null, Color? lightDefault = null)
    {
        if (IsPlacingGhost)
        {
            var ghostColor = IsDragCollision
                ? Color.FromArgb(160, 244, 63, 94)
                : (IsDark ? Color.FromArgb(140, 56, 189, 248) : Color.FromArgb(170, 0, 136, 204));
            return new SolidColorBrush(ghostColor);
        }
        if (IsGlowPass)
        {
            var glowColor = IsDragCollision
                ? Color.FromArgb(160, 244, 63, 94)
                : (IsDark ? Color.FromArgb(100, 56, 189, 248) : Color.FromArgb(80, 0, 136, 204));
            return new SolidColorBrush(glowColor);
        }
        if (IsSelected)
        {
            var selColor = IsDragCollision
                ? Color.FromRgb(244, 63, 94)
                : (IsDark ? Color.FromRgb(56, 189, 248) : Color.FromRgb(0, 136, 204));
            return new SolidColorBrush(selColor);
        }

        if (IsDark)
        {
            return new SolidColorBrush(darkDefault ?? Tokens?.DeviceStroke ?? Color.FromRgb(226, 232, 240));
        }

        return new SolidColorBrush(lightDefault ?? Tokens?.DeviceStroke ?? Color.FromRgb(30, 41, 59));
    }

    public Pen GetStrokePen(double baseThickness = 2.4, Color? darkDefault = null, Color? lightDefault = null)
    {
        if (IsPlacingGhost)
        {
            var ghostColor = IsDragCollision
                ? Color.FromArgb(180, 244, 63, 94)
                : (IsDark ? Color.FromArgb(140, 56, 189, 248) : Color.FromArgb(170, 0, 136, 204));
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
                : (IsDark ? Color.FromArgb(100, 56, 189, 248) : Color.FromArgb(80, 0, 136, 204));
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
                : (IsDark ? Color.FromRgb(56, 189, 248) : Color.FromRgb(0, 136, 204));
            return new Pen(new SolidColorBrush(selColor), (baseThickness + 0.4) * Zoom)
            {
                LineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
        }

        var strokeColor = IsDark
            ? (darkDefault ?? Tokens?.DeviceStroke ?? Color.FromRgb(226, 232, 240))
            : (lightDefault ?? Tokens?.DeviceStroke ?? Color.FromRgb(30, 41, 59));

        return new Pen(new SolidColorBrush(strokeColor), baseThickness * Zoom)
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
    }

    public IBrush? GetComponentFillBrush(Color? darkFill = null, Color? lightFill = null)
    {
        if (IsGlowPass || IsPlacingGhost) return null;

        if (IsDark)
        {
            return new SolidColorBrush(darkFill ?? Tokens?.DeviceBodyFill ?? Color.FromRgb(15, 23, 42));
        }

        return new SolidColorBrush(lightFill ?? Tokens?.DeviceBodyFill ?? Color.FromRgb(255, 255, 255));
    }

    public IBrush? GetComponentSecondaryBrush(Color? darkFill = null, Color? lightFill = null)
    {
        if (IsGlowPass || IsPlacingGhost) return null;

        if (IsDark)
        {
            return new SolidColorBrush(darkFill ?? Tokens?.DeviceBodySecondary ?? Color.FromRgb(30, 41, 59));
        }

        return new SolidColorBrush(lightFill ?? Tokens?.DeviceBodySecondary ?? Color.FromRgb(241, 245, 249));
    }

    public IBrush GetLeadBrush(Color? darkLead = null, Color? lightLead = null)
    {
        if (IsSelected)
        {
            return GetStrokeBrush();
        }

        if (IsDark)
        {
            return new SolidColorBrush(darkLead ?? Tokens?.DeviceLead ?? Color.FromRgb(203, 213, 225));
        }

        return new SolidColorBrush(lightLead ?? Tokens?.DeviceLead ?? Color.FromRgb(51, 65, 85));
    }

    public Pen GetLeadPen(double baseThickness = 2.0, Color? darkLead = null, Color? lightLead = null)
    {
        return new Pen(GetLeadBrush(darkLead, lightLead), baseThickness * Zoom)
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
    }

    public IBrush GetSolderPadBrush(Color? darkPad = null, Color? lightPad = null)
    {
        if (IsPlacingGhost || IsSelected)
        {
            return GetStrokeBrush();
        }

        if (IsDark)
        {
            return new SolidColorBrush(darkPad ?? Color.FromRgb(240, 246, 255));
        }

        return new SolidColorBrush(lightPad ?? Color.FromRgb(51, 65, 85));
    }

    public IBrush GetBadgeBgBrush()
    {
        if (IsDark)
        {
            return new SolidColorBrush(Tokens?.BadgeBg ?? Color.FromArgb(235, 15, 23, 42));
        }

        return new SolidColorBrush(Tokens?.BadgeBg ?? Color.FromArgb(244, 255, 255, 255));
    }

    public Pen GetBadgeBorderPen(Color? accent = null)
    {
        double penWidth = Math.Max(0.8, 1.0 * Zoom);
        if (accent.HasValue)
        {
            return new Pen(new SolidColorBrush(accent.Value), penWidth);
        }

        var col = IsDark
            ? (Tokens?.BadgeBorder ?? Color.FromArgb(96, 51, 65, 85))
            : (Tokens?.BadgeBorder ?? Color.FromArgb(192, 203, 213, 225));

        return new Pen(new SolidColorBrush(col), penWidth);
    }

    public IBrush GetBadgeTextBrush(Color? accent = null)
    {
        if (accent.HasValue)
        {
            return new SolidColorBrush(accent.Value);
        }

        if (IsDark)
        {
            return new SolidColorBrush(Tokens?.BadgeText ?? Color.FromRgb(226, 232, 240));
        }

        return new SolidColorBrush(Tokens?.BadgeText ?? Color.FromRgb(15, 23, 42));
    }
}

public interface IDeviceRenderer
{
    void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext);
}
