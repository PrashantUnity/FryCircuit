using System;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace FryCircuit.UI.Theming;

/// <summary>
/// Strongly-typed theme and color tokens for FryCircuit schematic CAD, waveform scopes, and UI components.
/// Allows direct access by custom renderers (Canvas, Oscilloscope, Rotary Knob) without string lookups.
/// </summary>
public sealed record CircuitThemeTokens
{
    public bool IsDark { get; init; } = true;

    // Backgrounds & Surfaces
    public Color Bg { get; init; }
    public IBrush BgBrush => new ImmutableSolidColorBrush(Bg);

    public Color Surface { get; init; }
    public IBrush SurfaceBrush => new ImmutableSolidColorBrush(Surface);

    public Color SurfaceHover { get; init; }
    public IBrush SurfaceHoverBrush => new ImmutableSolidColorBrush(SurfaceHover);

    public Color SurfaceHigh { get; init; }
    public IBrush SurfaceHighBrush => new ImmutableSolidColorBrush(SurfaceHigh);

    public Color SurfaceLow { get; init; }
    public IBrush SurfaceLowBrush => new ImmutableSolidColorBrush(SurfaceLow);

    public Color HeaderBg { get; init; }
    public IBrush HeaderBgBrush => new ImmutableSolidColorBrush(HeaderBg);

    public Color PanelBg { get; init; }
    public IBrush PanelBgBrush => new ImmutableSolidColorBrush(PanelBg);

    // Borders
    public Color Border { get; init; }
    public IBrush BorderBrush => new ImmutableSolidColorBrush(Border);

    public Color BorderSubtle { get; init; }
    public IBrush BorderSubtleBrush => new ImmutableSolidColorBrush(BorderSubtle);

    public Color BorderLight { get; init; }
    public IBrush BorderLightBrush => new ImmutableSolidColorBrush(BorderLight);

    public Color FocusBorder { get; init; }
    public IBrush FocusBorderBrush => new ImmutableSolidColorBrush(FocusBorder);

    // Typography
    public Color Text { get; init; }
    public IBrush TextBrush => new ImmutableSolidColorBrush(Text);

    public Color TextWhite { get; init; }
    public IBrush TextWhiteBrush => new ImmutableSolidColorBrush(TextWhite);

    public Color TextDim { get; init; }
    public IBrush TextDimBrush => new ImmutableSolidColorBrush(TextDim);

    public Color TextMuted { get; init; }
    public IBrush TextMutedBrush => new ImmutableSolidColorBrush(TextMuted);

    // Accents & Signals
    public Color Accent { get; init; }
    public IBrush AccentBrush => new ImmutableSolidColorBrush(Accent);

    public Color AccentHover { get; init; }
    public IBrush AccentHoverBrush => new ImmutableSolidColorBrush(AccentHover);

    public Color AccentSubtle { get; init; }
    public IBrush AccentSubtleBrush => new ImmutableSolidColorBrush(AccentSubtle);

    public Color Secondary { get; init; }
    public IBrush SecondaryBrush => new ImmutableSolidColorBrush(Secondary);

    public Color Warning { get; init; }
    public IBrush WarningBrush => new ImmutableSolidColorBrush(Warning);

    public Color Danger { get; init; }
    public IBrush DangerBrush => new ImmutableSolidColorBrush(Danger);

    // Schematic Canvas Specific
    public Color CanvasBg { get; init; }
    public IBrush CanvasBgBrush => new ImmutableSolidColorBrush(CanvasBg);

    public Color CanvasDotMinor { get; init; }
    public IBrush CanvasDotMinorBrush => new ImmutableSolidColorBrush(CanvasDotMinor);

    public Color CanvasDotMajor { get; init; }
    public IBrush CanvasDotMajorBrush => new ImmutableSolidColorBrush(CanvasDotMajor);

    public Color WireNormal { get; init; }
    public IBrush WireNormalBrush => new ImmutableSolidColorBrush(WireNormal);

    public Color WireActive { get; init; }
    public IBrush WireActiveBrush => new ImmutableSolidColorBrush(WireActive);

    public Color WireSelected { get; init; }
    public IBrush WireSelectedBrush => new ImmutableSolidColorBrush(WireSelected);

    // Oscilloscope Specific
    public Color ScopeBg { get; init; }
    public IBrush ScopeBgBrush => new ImmutableSolidColorBrush(ScopeBg);

    public Color ScopeGrid { get; init; }
    public IBrush ScopeGridBrush => new ImmutableSolidColorBrush(ScopeGrid);

    public Color ScopeCenter { get; init; }
    public IBrush ScopeCenterBrush => new ImmutableSolidColorBrush(ScopeCenter);

    public Color ScopeTick { get; init; }
    public IBrush ScopeTickBrush => new ImmutableSolidColorBrush(ScopeTick);

    public Color ScopeCh1 { get; init; }
    public IBrush ScopeCh1Brush => new ImmutableSolidColorBrush(ScopeCh1);

    public Color ScopeCh2 { get; init; }
    public IBrush ScopeCh2Brush => new ImmutableSolidColorBrush(ScopeCh2);

    public Color ScopeCh3 { get; init; }
    public IBrush ScopeCh3Brush => new ImmutableSolidColorBrush(ScopeCh3);

    public Color ScopeCh4 { get; init; }
    public IBrush ScopeCh4Brush => new ImmutableSolidColorBrush(ScopeCh4);

    public Color ScopeCh5 { get; init; }
    public IBrush ScopeCh5Brush => new ImmutableSolidColorBrush(ScopeCh5);

    public Color ScopeCh6 { get; init; }
    public IBrush ScopeCh6Brush => new ImmutableSolidColorBrush(ScopeCh6);

    public Color ScopeCh7 { get; init; }
    public IBrush ScopeCh7Brush => new ImmutableSolidColorBrush(ScopeCh7);

    public Color ScopeCh8 { get; init; }
    public IBrush ScopeCh8Brush => new ImmutableSolidColorBrush(ScopeCh8);

    // Hardware Controls
    public Color KnobTrack { get; init; }
    public IBrush KnobTrackBrush => new ImmutableSolidColorBrush(KnobTrack);

    public Color KnobBezel { get; init; }
    public IBrush KnobBezelBrush => new ImmutableSolidColorBrush(KnobBezel);

    // Semantic Device & Badge Tokens
    public Color DeviceBodyFill { get; init; }
    public IBrush DeviceBodyFillBrush => new ImmutableSolidColorBrush(DeviceBodyFill);

    public Color DeviceBodySecondary { get; init; }
    public IBrush DeviceBodySecondaryBrush => new ImmutableSolidColorBrush(DeviceBodySecondary);

    public Color DeviceStroke { get; init; }
    public IBrush DeviceStrokeBrush => new ImmutableSolidColorBrush(DeviceStroke);

    public Color DeviceLead { get; init; }
    public IBrush DeviceLeadBrush => new ImmutableSolidColorBrush(DeviceLead);

    public Color BadgeBg { get; init; }
    public IBrush BadgeBgBrush => new ImmutableSolidColorBrush(BadgeBg);

    public Color BadgeBorder { get; init; }
    public IBrush BadgeBorderBrush => new ImmutableSolidColorBrush(BadgeBorder);

    public Color BadgeText { get; init; }
    public IBrush BadgeTextBrush => new ImmutableSolidColorBrush(BadgeText);

    public static CircuitThemeTokens DarkDefaults => new()
    {
        IsDark = true,
        Bg = Color.FromRgb(0x08, 0x0C, 0x14),
        Surface = Color.FromRgb(0x0F, 0x17, 0x2A),
        SurfaceHover = Color.FromRgb(0x1E, 0x29, 0x3B),
        SurfaceHigh = Color.FromRgb(0x1E, 0x29, 0x3B),
        SurfaceLow = Color.FromRgb(0x0B, 0x11, 0x20),
        HeaderBg = Color.FromRgb(0x0B, 0x11, 0x20),
        PanelBg = Color.FromRgb(0x0F, 0x17, 0x2A),

        Border = Color.FromRgb(0x1E, 0x29, 0x3B),
        BorderSubtle = Color.FromRgb(0x15, 0x1E, 0x2E),
        BorderLight = Color.FromRgb(0x33, 0x41, 0x55),
        FocusBorder = Color.FromArgb(0x80, 0x38, 0xBD, 0xF8),

        Text = Color.FromRgb(0xE2, 0xE8, 0xF0),
        TextWhite = Color.FromRgb(0xF8, 0xFA, 0xFC),
        TextDim = Color.FromRgb(0x94, 0xA3, 0xB8),
        TextMuted = Color.FromRgb(0x64, 0x74, 0x8B),

        Accent = Color.FromRgb(0x38, 0xBD, 0xF8),
        AccentHover = Color.FromRgb(0x7D, 0xD3, 0xFC),
        AccentSubtle = Color.FromArgb(0x1F, 0x38, 0xBD, 0xF8),
        Secondary = Color.FromRgb(0x10, 0xB9, 0x81),
        Warning = Color.FromRgb(0xF5, 0x9E, 0x0B),
        Danger = Color.FromRgb(0xF4, 0x3F, 0x5E),

        CanvasBg = Color.FromRgb(0x08, 0x0C, 0x14),
        CanvasDotMinor = Color.FromRgb(0x15, 0x1F, 0x30),
        CanvasDotMajor = Color.FromArgb(0x26, 0x38, 0xBD, 0xF8),
        WireNormal = Color.FromRgb(0x64, 0x74, 0x8B),
        WireActive = Color.FromRgb(0x10, 0xB9, 0x81),
        WireSelected = Color.FromRgb(0x38, 0xBD, 0xF8),

        ScopeBg = Color.FromRgb(0x07, 0x0B, 0x12),
        ScopeGrid = Color.FromRgb(0x14, 0x20, 0x32),
        ScopeCenter = Color.FromRgb(0x1E, 0x31, 0x4B),
        ScopeTick = Color.FromRgb(0x28, 0x42, 0x63),
        ScopeCh1 = Color.FromRgb(0xF5, 0x9E, 0x0B),
        ScopeCh2 = Color.FromRgb(0x06, 0xB6, 0xD4),
        ScopeCh3 = Color.FromRgb(0xEC, 0x48, 0x99),
        ScopeCh4 = Color.FromRgb(0x10, 0xB9, 0x81),
        ScopeCh5 = Color.FromRgb(0x8B, 0x5C, 0xF6),
        ScopeCh6 = Color.FromRgb(0xF9, 0x73, 0x16),
        ScopeCh7 = Color.FromRgb(0x3B, 0x82, 0xF6),
        ScopeCh8 = Color.FromRgb(0x84, 0xCC, 0x16),

        KnobTrack = Color.FromRgb(0x16, 0x20, 0x32),
        KnobBezel = Color.FromRgb(0x24, 0x32, 0x48),

        DeviceBodyFill = Color.FromRgb(0x0F, 0x17, 0x2A),
        DeviceBodySecondary = Color.FromRgb(0x1E, 0x29, 0x3B),
        DeviceStroke = Color.FromRgb(0xE2, 0xE8, 0xF0),
        DeviceLead = Color.FromRgb(0xCB, 0xD5, 0xE1),
        BadgeBg = Color.FromArgb(0xEB, 0x0F, 0x17, 0x2A),
        BadgeBorder = Color.FromArgb(0x60, 0x33, 0x41, 0x55),
        BadgeText = Color.FromRgb(0xE2, 0xE8, 0xF0),
    };

    public static CircuitThemeTokens LightDefaults => new()
    {
        IsDark = false,
        Bg = Color.FromRgb(0xF4, 0xF7, 0xFA),
        Surface = Color.FromRgb(0xFF, 0xFF, 0xFF),
        SurfaceHover = Color.FromRgb(0xEA, 0xF0, 0xF6),
        SurfaceHigh = Color.FromRgb(0xF0, 0xF4, 0xF9),
        SurfaceLow = Color.FromRgb(0xF8, 0xFA, 0xFC),
        HeaderBg = Color.FromRgb(0xFF, 0xFF, 0xFF),
        PanelBg = Color.FromRgb(0xFF, 0xFF, 0xFF),

        Border = Color.FromRgb(0xD6, 0xDF, 0xE8),
        BorderSubtle = Color.FromRgb(0xE6, 0xEC, 0xF2),
        BorderLight = Color.FromRgb(0xCB, 0xD5, 0xE1),
        FocusBorder = Color.FromArgb(0xB3, 0x00, 0x88, 0xCC),

        Text = Color.FromRgb(0x1E, 0x29, 0x3B),
        TextWhite = Color.FromRgb(0x0F, 0x17, 0x2A),
        TextDim = Color.FromRgb(0x47, 0x55, 0x69),
        TextMuted = Color.FromRgb(0x64, 0x74, 0x8B),

        Accent = Color.FromRgb(0x00, 0x88, 0xCC),
        AccentHover = Color.FromRgb(0x00, 0x73, 0xB0),
        AccentSubtle = Color.FromArgb(0x15, 0x00, 0x88, 0xCC),
        Secondary = Color.FromRgb(0x05, 0x96, 0x69),
        Warning = Color.FromRgb(0xD9, 0x77, 0x06),
        Danger = Color.FromRgb(0xDC, 0x26, 0x26),

        // Soothing, glare-free matte blueprint paper tone
        CanvasBg = Color.FromRgb(0xF0, 0xF4, 0xF8),
        CanvasDotMinor = Color.FromRgb(0xCB, 0xD5, 0xE1),
        CanvasDotMajor = Color.FromArgb(0x33, 0x47, 0x55, 0x69),
        WireNormal = Color.FromRgb(0x47, 0x55, 0x69),
        WireActive = Color.FromRgb(0x05, 0x96, 0x69),
        WireSelected = Color.FromRgb(0x00, 0x88, 0xCC),

        // Modern laboratory instrument inverted graticule
        ScopeBg = Color.FromRgb(0xF1, 0xF5, 0xF9),
        ScopeGrid = Color.FromRgb(0xE2, 0xE8, 0xF0),
        ScopeCenter = Color.FromRgb(0xCB, 0xD5, 0xE1),
        ScopeTick = Color.FromRgb(0x94, 0xA3, 0xB8),
        ScopeCh1 = Color.FromRgb(0xD9, 0x77, 0x06), // Rich Amber Gold
        ScopeCh2 = Color.FromRgb(0x02, 0x84, 0xC7), // Oceanic Azure Cyan
        ScopeCh3 = Color.FromRgb(0xE1, 0x1D, 0x48), // Ruby Rose
        ScopeCh4 = Color.FromRgb(0x05, 0x96, 0x69), // Forest Emerald
        ScopeCh5 = Color.FromRgb(0x7C, 0x3A, 0xED),
        ScopeCh6 = Color.FromRgb(0xEA, 0x58, 0x0C),
        ScopeCh7 = Color.FromRgb(0x25, 0x63, 0xEB),
        ScopeCh8 = Color.FromRgb(0x65, 0xA3, 0x0D),

        KnobTrack = Color.FromRgb(0xE2, 0xE8, 0xF0),
        KnobBezel = Color.FromRgb(0xCB, 0xD5, 0xE1),

        DeviceBodyFill = Color.FromRgb(0xFF, 0xFF, 0xFF),
        DeviceBodySecondary = Color.FromRgb(0xF1, 0xF5, 0xF9),
        DeviceStroke = Color.FromRgb(0x1E, 0x29, 0x3B),
        DeviceLead = Color.FromRgb(0x33, 0x41, 0x55),
        BadgeBg = Color.FromArgb(0xF4, 0xFF, 0xFF, 0xFF),
        BadgeBorder = Color.FromArgb(0xC0, 0xCB, 0xD5, 0xE1),
        BadgeText = Color.FromRgb(0x0F, 0x17, 0x2A),
    };

    public static Color GetDefaultChannelColor(int channelIndex, bool isDark = true)
    {
        if (isDark)
        {
            return (channelIndex % 8) switch
            {
                1 => Color.FromRgb(245, 158, 11),  // CH1 Amber Gold
                2 => Color.FromRgb(6, 182, 212),   // CH2 Precision Cyan
                3 => Color.FromRgb(236, 72, 153),  // CH3 Magenta Pink
                4 => Color.FromRgb(16, 185, 129),  // CH4 Emerald Green
                5 => Color.FromRgb(139, 92, 246),  // CH5 Violet Purple
                6 => Color.FromRgb(249, 115, 22),  // CH6 Vivid Orange
                7 => Color.FromRgb(59, 130, 246),  // CH7 Electric Blue
                0 => Color.FromRgb(132, 204, 22),  // CH8 Lime Green
                _ => Color.FromRgb(245, 158, 11)
            };
        }

        return (channelIndex % 8) switch
        {
            1 => Color.FromRgb(217, 119, 6),    // CH1 Rich Amber Gold (#D97706)
            2 => Color.FromRgb(2, 132, 199),    // CH2 Precision Cyan Blue (#0284C7)
            3 => Color.FromRgb(225, 29, 72),    // CH3 Ruby Rose (#E11D48)
            4 => Color.FromRgb(5, 150, 105),    // CH4 Forest Emerald (#059669)
            5 => Color.FromRgb(124, 58, 237),   // CH5 Amethyst Violet (#7C3AED)
            6 => Color.FromRgb(234, 88, 12),    // CH6 Burnt Rust Orange (#EA580C)
            7 => Color.FromRgb(37, 99, 235),    // CH7 Cobalt Blue (#2563EB)
            0 => Color.FromRgb(101, 163, 13),   // CH8 Olive Moss (#65A30D)
            _ => Color.FromRgb(217, 119, 6)
        };
    }

    public Dictionary<object, object?> ToDictionary()
    {
        var dict = new Dictionary<object, object?>();

        void Put(string name, Color color)
        {
            var brush = new ImmutableSolidColorBrush(color);
            dict[name + "Color"] = color;
            dict[name + "Brush"] = brush;
            dict[name] = brush;
        }

        Put("CircuitBg", Bg);
        Put("CircuitSurface", Surface);
        Put("CircuitSurfaceHover", SurfaceHover);
        Put("CircuitSurfaceHigh", SurfaceHigh);
        Put("CircuitSurfaceLow", SurfaceLow);
        Put("CircuitHeaderBg", HeaderBg);
        Put("CircuitPanelBg", PanelBg);

        Put("CircuitBorder", Border);
        Put("CircuitBorderSubtle", BorderSubtle);
        Put("CircuitBorderLight", BorderLight);
        Put("CircuitFocusBorder", FocusBorder);

        Put("CircuitText", Text);
        Put("CircuitTextWhite", TextWhite);
        Put("CircuitTextDim", TextDim);
        Put("CircuitTextMuted", TextMuted);

        Put("CircuitPrimary", Accent);
        Put("CircuitPrimaryHover", AccentHover);
        Put("CircuitAccent", Accent);
        Put("CircuitAccentHover", AccentHover);
        Put("CircuitAccentSubtle", AccentSubtle);
        Put("CircuitSecondary", Secondary);
        Put("CircuitWarning", Warning);
        Put("CircuitDanger", Danger);

        Put("CircuitCanvas", CanvasBg);
        Put("CircuitCanvasBg", CanvasBg);
        Put("CircuitCanvasDotMinor", CanvasDotMinor);
        Put("CircuitCanvasDotMajor", CanvasDotMajor);
        Put("CircuitWire", WireNormal);
        Put("CircuitWireNormal", WireNormal);
        Put("CircuitWireActive", WireActive);
        Put("CircuitWireSelected", WireSelected);

        Put("CircuitScopeBg", ScopeBg);
        Put("CircuitScopeGrid", ScopeGrid);
        Put("CircuitScopeCenter", ScopeCenter);
        Put("CircuitScopeTick", ScopeTick);
        Put("CircuitScopeCh1", ScopeCh1);
        Put("CircuitScopeCh2", ScopeCh2);
        Put("CircuitScopeCh3", ScopeCh3);
        Put("CircuitScopeCh4", ScopeCh4);
        Put("CircuitScopeCh5", ScopeCh5);
        Put("CircuitScopeCh6", ScopeCh6);
        Put("CircuitScopeCh7", ScopeCh7);
        Put("CircuitScopeCh8", ScopeCh8);

        Put("CircuitKnobTrack", KnobTrack);
        Put("CircuitKnobBezel", KnobBezel);

        Put("CircuitDeviceBodyFill", DeviceBodyFill);
        Put("CircuitDeviceBodySecondary", DeviceBodySecondary);
        Put("CircuitDeviceStroke", DeviceStroke);
        Put("CircuitDeviceLead", DeviceLead);
        Put("CircuitBadgeBg", BadgeBg);
        Put("CircuitBadgeBorder", BadgeBorder);
        Put("CircuitBadgeText", BadgeText);

        return dict;
    }
}
