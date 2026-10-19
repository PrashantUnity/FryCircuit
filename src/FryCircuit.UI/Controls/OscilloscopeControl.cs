using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using FryCircuit.Core.Models;
using FryCircuit.UI.Theming;

namespace FryCircuit.UI.Controls;

public enum OscilloscopeDisplayMode
{
    TimeDomain,
    FftSpectrum
}

public class OscilloscopeControl : Control
{
    public ObservableCollection<OscilloscopeChannel> Channels { get; } = new();
    public ObservableCollection<OscilloscopeChannel> Traces => Channels;

    public static readonly StyledProperty<double> TimebaseMsProperty =
        AvaloniaProperty.Register<OscilloscopeControl, double>(nameof(TimebaseMs), 10.0);

    public double TimebaseMs
    {
        get => GetValue(TimebaseMsProperty);
        set => SetValue(TimebaseMsProperty, value);
    }

    public OscilloscopeDisplayLayout DisplayLayout { get; set; } = OscilloscopeDisplayLayout.Overlay;

    public OscilloscopeDisplayMode DisplayMode
    {
        get => DisplayLayout == OscilloscopeDisplayLayout.FftSpectrum
            ? OscilloscopeDisplayMode.FftSpectrum
            : OscilloscopeDisplayMode.TimeDomain;
        set
        {
            if (value == OscilloscopeDisplayMode.FftSpectrum)
            {
                DisplayLayout = OscilloscopeDisplayLayout.FftSpectrum;
            }
            else if (DisplayLayout == OscilloscopeDisplayLayout.FftSpectrum)
            {
                DisplayLayout = OscilloscopeDisplayLayout.Overlay;
            }
        }
    }

    public bool ShowCursors { get; set; } = false;
    public double Cursor1X { get; set; } = 0.35; // Normalized 0..1
    public double Cursor2X { get; set; } = 0.65; // Normalized 0..1

    // Horizontal Pan & Zoom (Time Axis)
    public double TimeOffsetDivs { get; set; } = 0.0; // Horizontal shift in graticule divisions (-50.0 to +50.0)
    public double TimebaseZoom { get; set; } = 1.0;   // Horizontal zoom multiplier (0.05x to 100.0x)

    // Active Channel for Vertical Pan & Scale in Overlay Mode
    public int ActiveChannelIndex { get; set; } = 1;

    // Box / Marquee Zoom
    public bool IsBoxZoomMode { get; set; } = false;
    private bool _isDraggingBoxZoom = false;
    private Point _boxZoomStart;
    private Point _boxZoomEnd;

    public OscilloscopeTriggerSettings TriggerSettings { get; } = new();

    private int _draggingCursor = 0; // 0=none, 1=cursor1, 2=cursor2
    private int _draggingGroundChannelIndex = 0; // channel index being dragged
    private bool _draggingTrigger = false;
    private bool _isPanningWaveform = false;
    private Point _panStartPoint;
    private double _panStartTimeOffset;
    private double _panStartVerticalOffset;
    private int _panTargetChannelIndex = 1;
    private bool _draggingTimeMarker = false;

    private Rect _modeBtnRect;
    private Rect _layoutBtnRect;
    private Rect _triggerBtnRect;
    private Rect _cursorBtnRect;
    private Rect _boxZoomBtnRect;
    private Rect _autoSetBtnRect;
    private Rect _centerPanBtnRect;
    private Rect _triggerTabRect;
    private Rect _timePositionMarkerRect;

    private readonly List<Rect> _traceBadgeRects = new();
    private readonly Dictionary<int, Rect> _groundTabRects = new();

    public OscilloscopeControl()
    {
        ClipToBounds = true;
        MinHeight = 120;
        Focusable = true;
        CircuitThemeService.Instance.TokensChanged += _ => InvalidateVisual();

        // Standard 4 Industry Default Channels:
        // CH1: Amber Gold, CH2: Precision Cyan, CH3: Magenta Pink, CH4: Emerald Green
        Channels.Add(OscilloscopeChannel.CreateDefault(1));
        Channels.Add(OscilloscopeChannel.CreateDefault(2));
        Channels.Add(OscilloscopeChannel.CreateDefault(3));
        Channels.Add(OscilloscopeChannel.CreateDefault(4));
    }

    public void ResetPanAndZoom()
    {
        TimeOffsetDivs = 0.0;
        TimebaseZoom = 1.0;
        foreach (var ch in Channels)
        {
            ch.VerticalOffsetDivs = 0.0;
        }
        InvalidateVisual();
    }

    public void ZoomInTime()
    {
        TimebaseZoom = Math.Clamp(TimebaseZoom * 1.5, 0.05, 100.0);
        InvalidateVisual();
    }

    public void ZoomOutTime()
    {
        TimebaseZoom = Math.Clamp(TimebaseZoom / 1.5, 0.05, 100.0);
        InvalidateVisual();
    }

    public void PanTime(double divs)
    {
        TimeOffsetDivs = Math.Clamp(TimeOffsetDivs + divs, -50.0, 50.0);
        InvalidateVisual();
    }

    public void ZoomInVoltage(int? chIdx = null)
    {
        var ch = Channels.FirstOrDefault(c => c.ChannelIndex == (chIdx ?? ActiveChannelIndex)) ?? Channels.FirstOrDefault();
        if (ch != null)
        {
            ch.VoltsPerDiv = NextVoltsPerDiv(ch.VoltsPerDiv, zoomIn: true);
            InvalidateVisual();
        }
    }

    public void ZoomOutVoltage(int? chIdx = null)
    {
        var ch = Channels.FirstOrDefault(c => c.ChannelIndex == (chIdx ?? ActiveChannelIndex)) ?? Channels.FirstOrDefault();
        if (ch != null)
        {
            ch.VoltsPerDiv = NextVoltsPerDiv(ch.VoltsPerDiv, zoomIn: false);
            InvalidateVisual();
        }
    }

    public void PanVoltage(double divs, int? chIdx = null)
    {
        var ch = Channels.FirstOrDefault(c => c.ChannelIndex == (chIdx ?? ActiveChannelIndex)) ?? Channels.FirstOrDefault();
        if (ch != null)
        {
            ch.VerticalOffsetDivs = Math.Clamp(ch.VerticalOffsetDivs + divs, -15.0, 15.0);
            InvalidateVisual();
        }
    }

    public void ToggleBoxZoom()
    {
        IsBoxZoomMode = !IsBoxZoomMode;
        SafeSetCursor(IsBoxZoomMode ? StandardCursorType.Cross : StandardCursorType.Arrow);
        InvalidateVisual();
    }

    private void SafeSetCursor(StandardCursorType cursorType)
    {
        try
        {
            Cursor = new Cursor(cursorType);
        }
        catch (InvalidOperationException)
        {
            // Headless unit test runner without ICursorFactory platform service
        }
    }

    public static double NextVoltsPerDiv(double current, bool zoomIn)
    {
        double[] steps = { 1e-4, 2e-4, 5e-4, 1e-3, 2e-3, 5e-3, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1.0, 2.0, 5.0, 10.0, 20.0, 50.0, 100.0, 200.0, 500.0, 1000.0 };
        if (zoomIn)
        {
            for (int i = steps.Length - 1; i >= 0; i--)
            {
                if (steps[i] < current - 1e-9) return steps[i];
            }
            return steps[0];
        }
        else
        {
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i] > current + 1e-9) return steps[i];
            }
            return steps[^1];
        }
    }

    public void PushSamples(double ch1Value, double ch2Value)
    {
        if (Channels.Count > 0) Channels[0].AddSample(ch1Value);
        if (Channels.Count > 1) Channels[1].AddSample(ch2Value);
        InvalidateVisual();
    }

    public void PushSamples(IReadOnlyDictionary<int, double> channelSamples)
    {
        foreach (var ch in Channels)
        {
            if (channelSamples.TryGetValue(ch.ChannelIndex, out double val))
            {
                ch.AddSample(val);
            }
            else if (ch.TargetNodeId.HasValue && channelSamples.TryGetValue(ch.TargetNodeId.Value, out double nodeVal))
            {
                ch.AddSample(nodeVal);
            }
        }
        InvalidateVisual();
    }

    public void PushSamples(params double[] channelValues)
    {
        for (int i = 0; i < channelValues.Length && i < Channels.Count; i++)
        {
            Channels[i].AddSample(channelValues[i]);
        }
        InvalidateVisual();
    }

    public void AutoSet()
    {
        TimeOffsetDivs = 0.0;
        TimebaseZoom = 1.0;
        foreach (var ch in Channels)
        {
            if (ch.Samples.Count < 2) continue;
            double peak = ch.Samples.Max(s => Math.Abs(s));
            double niceMax = GetEngineeringNiceMax(peak);
            ch.VoltsPerDiv = Math.Clamp(niceMax / 4.0, 1e-4, 1000.0);
            ch.VerticalOffsetDivs = 0.0;
        }
        InvalidateVisual();
    }

    private int GetChannelIndexAtPoint(Point pt, double headerH, double screenH)
    {
        if (DisplayLayout == OscilloscopeDisplayLayout.Stacked)
        {
            var active = Channels.Where(c => c.IsVisible).ToList();
            if (active.Count == 0) active = Channels.Take(2).ToList();
            double paneH = screenH / Math.Max(1, active.Count);
            int paneIdx = (int)((pt.Y - headerH) / Math.Max(1.0, paneH));
            paneIdx = Math.Clamp(paneIdx, 0, active.Count - 1);
            return active[paneIdx].ChannelIndex;
        }
        return ActiveChannelIndex;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pt = e.GetPosition(this);
        double w = Bounds.Width;
        double h = Bounds.Height;
        double headerH = 26.0;
        double screenH = Math.Max(10.0, h - headerH);

        // 1. Channel Badge Toggle (Click to Mute / Unmute, Double-Click to Reset Vertical Offset)
        for (int i = 0; i < _traceBadgeRects.Count && i < Channels.Count; i++)
        {
            if (_traceBadgeRects[i].Contains(pt))
            {
                ActiveChannelIndex = Channels[i].ChannelIndex;
                if (e.ClickCount >= 2)
                {
                    Channels[i].VerticalOffsetDivs = 0.0;
                }
                else
                {
                    Channels[i].IsVisible = !Channels[i].IsVisible;
                }
                InvalidateVisual();
                e.Handled = true;
                return;
            }
        }

        // 2. Toolbar Action Buttons
        if (_autoSetBtnRect.Contains(pt))
        {
            AutoSet();
            e.Handled = true;
            return;
        }

        if (_centerPanBtnRect.Contains(pt))
        {
            ResetPanAndZoom();
            e.Handled = true;
            return;
        }

        if (_boxZoomBtnRect.Contains(pt))
        {
            ToggleBoxZoom();
            e.Handled = true;
            return;
        }

        if (_layoutBtnRect.Contains(pt))
        {
            DisplayLayout = DisplayLayout == OscilloscopeDisplayLayout.Overlay
                ? OscilloscopeDisplayLayout.Stacked
                : OscilloscopeDisplayLayout.Overlay;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_modeBtnRect.Contains(pt))
        {
            DisplayLayout = DisplayLayout switch
            {
                OscilloscopeDisplayLayout.Overlay or OscilloscopeDisplayLayout.Stacked => OscilloscopeDisplayLayout.FftSpectrum,
                OscilloscopeDisplayLayout.FftSpectrum => OscilloscopeDisplayLayout.XY,
                _ => OscilloscopeDisplayLayout.Overlay
            };
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_triggerBtnRect.Contains(pt))
        {
            TriggerSettings.Mode = TriggerSettings.Mode switch
            {
                TriggerMode.Auto => TriggerMode.Normal,
                TriggerMode.Normal => TriggerMode.Single,
                _ => TriggerMode.Auto
            };
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_cursorBtnRect.Contains(pt))
        {
            ShowCursors = !ShowCursors;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        // 3. Ground Offset Tab Dragging
        foreach (var (chIdx, rect) in _groundTabRects)
        {
            if (rect.Contains(pt))
            {
                _draggingGroundChannelIndex = chIdx;
                ActiveChannelIndex = chIdx;
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }
        }

        // 4. Trigger Level Tab Dragging
        if (_triggerTabRect.Contains(pt))
        {
            _draggingTrigger = true;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        // 5. Cursors Dragging
        if (ShowCursors && (DisplayLayout == OscilloscopeDisplayLayout.Overlay || DisplayLayout == OscilloscopeDisplayLayout.Stacked))
        {
            double c1Screen = Cursor1X * w;
            double c2Screen = Cursor2X * w;

            if (Math.Abs(pt.X - c1Screen) < 14)
            {
                _draggingCursor = 1;
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }
            if (Math.Abs(pt.X - c2Screen) < 14)
            {
                _draggingCursor = 2;
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }
        }

        // 6. Time Position Marker Dragging [▼]
        if (_timePositionMarkerRect.Width > 0 && _timePositionMarkerRect.Contains(pt))
        {
            _draggingTimeMarker = true;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        // 7. Interactive Waveform Box Zoom or Panning across both axes
        if (pt.Y > headerH && (DisplayLayout == OscilloscopeDisplayLayout.Overlay || DisplayLayout == OscilloscopeDisplayLayout.Stacked || DisplayLayout == OscilloscopeDisplayLayout.XY))
        {
            if (e.ClickCount >= 2)
            {
                TimeOffsetDivs = 0.0;
                TimebaseZoom = 1.0;
                int chIdx = GetChannelIndexAtPoint(pt, headerH, screenH);
                var target = Channels.FirstOrDefault(c => c.ChannelIndex == chIdx);
                if (target != null) target.VerticalOffsetDivs = 0.0;
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            bool isRightClick = e.GetCurrentPoint(this).Properties.IsRightButtonPressed;
            if (isRightClick || IsBoxZoomMode || (e.KeyModifiers & KeyModifiers.Shift) != 0)
            {
                _isDraggingBoxZoom = true;
                _boxZoomStart = pt;
                _boxZoomEnd = pt;
                e.Pointer.Capture(this);
                SafeSetCursor(StandardCursorType.Cross);
                e.Handled = true;
                return;
            }

            _isPanningWaveform = true;
            _panStartPoint = pt;
            _panStartTimeOffset = TimeOffsetDivs;
            _panTargetChannelIndex = GetChannelIndexAtPoint(pt, headerH, screenH);
            ActiveChannelIndex = _panTargetChannelIndex;
            var targetCh = Channels.FirstOrDefault(c => c.ChannelIndex == _panTargetChannelIndex);
            _panStartVerticalOffset = targetCh?.VerticalOffsetDivs ?? 0.0;
            e.Pointer.Capture(this);
            SafeSetCursor(StandardCursorType.SizeAll);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pt = e.GetPosition(this);
        double w = Bounds.Width;
        double h = Bounds.Height;
        double headerH = 26.0;
        double screenH = Math.Max(10.0, h - headerH);
        double centerY = headerH + screenH / 2.0;

        if (_isDraggingBoxZoom)
        {
            _boxZoomEnd = pt;
            InvalidateVisual();
        }
        else if (_isPanningWaveform)
        {
            double marginL = 36.0;
            double marginR = 36.0;
            double plotW = Math.Max(10.0, w - marginL - marginR);
            double divX = plotW / 10.0;
            double dx = pt.X - _panStartPoint.X;
            double dy = pt.Y - _panStartPoint.Y;

            // 1. Horizontal Pan (Time Offset)
            TimeOffsetDivs = Math.Clamp(_panStartTimeOffset + (dx / Math.Max(1.0, divX)), -50.0, 50.0);

            // 2. Vertical Pan (Channel Voltage Offset)
            var targetCh = Channels.FirstOrDefault(c => c.ChannelIndex == _panTargetChannelIndex);
            if (targetCh != null)
            {
                double divY = DisplayLayout == OscilloscopeDisplayLayout.Stacked
                    ? (screenH / Math.Max(1, Channels.Count(c => c.IsVisible))) / 4.0
                    : screenH / 8.0;

                double newOffset = _panStartVerticalOffset - (dy / Math.Max(1.0, divY));
                targetCh.VerticalOffsetDivs = Math.Clamp(newOffset, -15.0, 15.0);
            }

            InvalidateVisual();
        }
        else if (_draggingTimeMarker)
        {
            double marginL = 36.0;
            double marginR = 36.0;
            double plotW = Math.Max(10.0, w - marginL - marginR);
            double centerX = marginL + plotW / 2.0;
            double divX = plotW / 10.0;
            TimeOffsetDivs = Math.Clamp((pt.X - centerX) / Math.Max(1.0, divX), -50.0, 50.0);
            InvalidateVisual();
        }
        else if (_draggingCursor != 0)
        {
            double ratio = Math.Clamp(pt.X / Math.Max(1.0, w), 0.02, 0.98);
            if (_draggingCursor == 1) Cursor1X = ratio;
            else if (_draggingCursor == 2) Cursor2X = ratio;
            InvalidateVisual();
        }
        else if (_draggingGroundChannelIndex > 0)
        {
            var ch = Channels.FirstOrDefault(c => c.ChannelIndex == _draggingGroundChannelIndex);
            if (ch != null)
            {
                double divY = DisplayLayout == OscilloscopeDisplayLayout.Stacked
                    ? (screenH / Math.Max(1, Channels.Count(c => c.IsVisible))) / 4.0
                    : screenH / 8.0;
                double offset = (centerY - pt.Y) / Math.Max(1e-6, divY);
                ch.VerticalOffsetDivs = Math.Clamp(offset, -15.0, 15.0);
                InvalidateVisual();
            }
        }
        else if (_draggingTrigger)
        {
            var trigCh = Channels.FirstOrDefault(c => c.ChannelIndex == TriggerSettings.SourceChannelIndex) ?? Channels.FirstOrDefault();
            if (trigCh != null)
            {
                double divY = screenH / 8.0;
                double vPerDiv = trigCh.VoltsPerDiv > 1e-9 ? trigCh.VoltsPerDiv : 1.0;
                double scaleY = divY / vPerDiv;
                double offsetPx = trigCh.VerticalOffsetDivs * divY;
                double level = (centerY - offsetPx - pt.Y) / Math.Max(1e-9, scaleY);
                TriggerSettings.Level = level;
                InvalidateVisual();
            }
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isDraggingBoxZoom)
        {
            _isDraggingBoxZoom = false;
            SafeSetCursor(StandardCursorType.Arrow);
            e.Pointer.Capture(null);

            double minX = Math.Min(_boxZoomStart.X, _boxZoomEnd.X);
            double maxX = Math.Max(_boxZoomStart.X, _boxZoomEnd.X);
            double minY = Math.Min(_boxZoomStart.Y, _boxZoomEnd.Y);
            double maxY = Math.Max(_boxZoomStart.Y, _boxZoomEnd.Y);

            double bw = maxX - minX;
            double bh = maxY - minY;

            if (bw > 10 && bh > 10)
            {
                double marginL = 36.0;
                double marginR = 36.0;
                double plotW = Math.Max(10.0, Bounds.Width - marginL - marginR);
                double plotCenterX = marginL + plotW / 2.0;
                double divX = plotW / 10.0;

                // Horizontal Zoom
                double oldZoom = TimebaseZoom;
                double zoomRatioX = Math.Clamp(plotW / bw, 1.0, 50.0);
                TimebaseZoom = Math.Clamp(oldZoom * zoomRatioX, 0.05, 100.0);

                double boxCenterX = (minX + maxX) / 2.0;
                double offsetShiftX = (plotCenterX - boxCenterX) / divX;
                TimeOffsetDivs = Math.Clamp(TimeOffsetDivs + offsetShiftX, -50.0, 50.0);

                // Vertical Zoom for Active Channel
                var targetCh = Channels.FirstOrDefault(c => c.ChannelIndex == ActiveChannelIndex) ?? Channels.FirstOrDefault();
                if (targetCh != null)
                {
                    double headerH = 26.0;
                    double screenH = Math.Max(10.0, Bounds.Height - headerH);
                    double centerY = headerH + screenH / 2.0;
                    double divY = screenH / 8.0;

                    double zoomRatioY = bh / screenH;
                    targetCh.VoltsPerDiv = Math.Clamp(targetCh.VoltsPerDiv * zoomRatioY, 1e-4, 1e4);

                    double boxCenterY = (minY + maxY) / 2.0;
                    double offsetShiftY = (centerY - boxCenterY) / divY;
                    targetCh.VerticalOffsetDivs = Math.Clamp(targetCh.VerticalOffsetDivs + offsetShiftY, -15.0, 15.0);
                }
            }

            IsBoxZoomMode = false;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isPanningWaveform)
        {
            _isPanningWaveform = false;
            SafeSetCursor(StandardCursorType.Arrow);
            e.Pointer.Capture(null);
            InvalidateVisual();
        }
        if (_draggingTimeMarker)
        {
            _draggingTimeMarker = false;
            e.Pointer.Capture(null);
            InvalidateVisual();
        }
        if (_draggingCursor != 0 || _draggingGroundChannelIndex > 0 || _draggingTrigger)
        {
            _draggingCursor = 0;
            _draggingGroundChannelIndex = 0;
            _draggingTrigger = false;
            e.Pointer.Capture(null);
            InvalidateVisual();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var pt = e.GetPosition(this);
        double deltaY = e.Delta.Y;
        double deltaX = e.Delta.X;

        double headerH = 26.0;
        double screenH = Math.Max(10.0, Bounds.Height - headerH);
        double marginL = 36.0;
        double marginR = 36.0;
        double plotW = Math.Max(10.0, Bounds.Width - marginL - marginR);

        bool isCtrl = (e.KeyModifiers & KeyModifiers.Control) != 0;
        bool isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        bool isAlt = (e.KeyModifiers & KeyModifiers.Alt) != 0;

        // 1. Wheel over Left Voltage Scale: Zoom Volts/Div of Active Channel
        if (pt.X <= marginL)
        {
            var vCh = Channels.FirstOrDefault(c => c.ChannelIndex == ActiveChannelIndex) ?? Channels.FirstOrDefault();
            if (vCh != null)
            {
                vCh.VoltsPerDiv = NextVoltsPerDiv(vCh.VoltsPerDiv, zoomIn: deltaY > 0);
                InvalidateVisual();
                e.Handled = true;
                return;
            }
        }

        // 2. Alt+Wheel: Vertical Pan (Shift Voltage Offset)
        if (isAlt)
        {
            int vertChIdx = GetChannelIndexAtPoint(pt, headerH, screenH);
            var vCh = Channels.FirstOrDefault(c => c.ChannelIndex == vertChIdx);
            if (vCh != null)
            {
                vCh.VerticalOffsetDivs = Math.Clamp(vCh.VerticalOffsetDivs + deltaY * 0.25, -15.0, 15.0);
                InvalidateVisual();
                e.Handled = true;
                return;
            }
        }

        // 3. Shift+Wheel or Horizontal Trackpad Swipe: Horizontal Pan (Scrub Time)
        if (isShift || Math.Abs(deltaX) > 0.01)
        {
            double scrollPan = (isShift ? deltaY : -deltaX) * 0.4;
            TimeOffsetDivs = Math.Clamp(TimeOffsetDivs + scrollPan, -50.0, 50.0);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        // 4. Default Wheel over Main Graticule: Natural Timebase Zoom Centered at Mouse!
        double oldZoom = TimebaseZoom;
        double zoomFactor = deltaY > 0 ? 1.25 : 0.8;
        double newZoom = Math.Clamp(oldZoom * zoomFactor, 0.05, 100.0);

        double centerX = marginL + plotW / 2.0;
        double cursorDivFromCenter = (pt.X - centerX) / (plotW / 10.0);
        TimeOffsetDivs += cursorDivFromCenter * (1.0 - newZoom / oldZoom);
        TimeOffsetDivs = Math.Clamp(TimeOffsetDivs, -50.0, 50.0);
        TimebaseZoom = newZoom;
        InvalidateVisual();
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 10 || h <= 10) return;

        // Background: Deep dark CRT phosphor bezel with subtle gradient
        var bgBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(10, 18, 25), 0.0),
                new GradientStop(Color.FromRgb(7, 12, 17), 0.5),
                new GradientStop(Color.FromRgb(4, 8, 12), 1.0)
            }
        };
        context.FillRectangle(bgBrush, new Rect(0, 0, w, h));

        // Compute toolbar action buttons
        ComputeInstrumentButtons(w);

        switch (DisplayLayout)
        {
            case OscilloscopeDisplayLayout.FftSpectrum:
                RenderFftSpectrum(context, w, h);
                break;
            case OscilloscopeDisplayLayout.Stacked:
                RenderStackedTimeDomain(context, w, h);
                break;
            case OscilloscopeDisplayLayout.XY:
                RenderXyDisplay(context, w, h);
                break;
            case OscilloscopeDisplayLayout.Overlay:
            default:
                RenderOverlayTimeDomain(context, w, h);
                break;
        }

        // Draw Box Zoom Marquee if active
        if (_isDraggingBoxZoom)
        {
            double bx = Math.Min(_boxZoomStart.X, _boxZoomEnd.X);
            double by = Math.Min(_boxZoomStart.Y, _boxZoomEnd.Y);
            double bw = Math.Abs(_boxZoomEnd.X - _boxZoomStart.X);
            double bh = Math.Abs(_boxZoomEnd.Y - _boxZoomStart.Y);

            var marqueeRect = new Rect(bx, by, bw, bh);
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(45, 0, 240, 255)), marqueeRect);
            var dashPen = new Pen(new SolidColorBrush(Color.FromArgb(230, 0, 240, 255)), 1.5)
            {
                DashStyle = DashStyle.Dash
            };
            context.DrawRectangle(dashPen, marqueeRect);

            var ftBox = new FormattedText(
                $"BOX ZOOM: {bw:F0}x{bh:F0}",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
                9.0,
                Brushes.White
            );
            context.DrawText(ftBox, new Point(bx + 4, Math.Max(26.0, by - ftBox.Height - 2)));
        }
    }

    private void ComputeInstrumentButtons(double w)
    {
        string modeLabel = DisplayLayout switch
        {
            OscilloscopeDisplayLayout.FftSpectrum => "MODE: FFT",
            OscilloscopeDisplayLayout.XY => "MODE: XY",
            _ => "MODE: TIME"
        };
        var ftMode = new FormattedText(
            modeLabel,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            9.5,
            Brushes.White
        );

        string layoutLabel = DisplayLayout == OscilloscopeDisplayLayout.Stacked ? "LAYOUT: STACKED" : "LAYOUT: OVERLAY";
        var ftLayout = new FormattedText(
            layoutLabel,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            9.5,
            Brushes.White
        );

        string trigLabel = $"TRIG: {TriggerSettings.Mode.ToString().ToUpperInvariant()}";
        var ftTrig = new FormattedText(
            trigLabel,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            9.5,
            Brushes.White
        );

        string cursorLabel = ShowCursors ? "CURSORS: ON" : "CURSORS: OFF";
        var ftCursor = new FormattedText(
            cursorLabel,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            9.5,
            Brushes.White
        );

        string boxZoomLabel = IsBoxZoomMode ? "BOX: ACTIVE" : "BOX ZOOM";
        var ftBoxBtn = new FormattedText(
            boxZoomLabel,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            9.5,
            Brushes.White
        );

        var ftAuto = new FormattedText(
            "AUTOSET",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            9.5,
            Brushes.White
        );

        double rightMargin = 10.0;
        double modeW = ftMode.Width + 12;
        double layoutW = ftLayout.Width + 12;
        double trigW = ftTrig.Width + 12;
        double cursorW = ftCursor.Width + 12;
        double boxZoomW = ftBoxBtn.Width + 12;
        double autoW = ftAuto.Width + 12;
        double centerW = 76.0;

        _autoSetBtnRect = new Rect(w - autoW - rightMargin, 3, autoW, 20);
        _centerPanBtnRect = new Rect(_autoSetBtnRect.X - centerW - 6, 3, centerW, 20);
        _boxZoomBtnRect = new Rect(_centerPanBtnRect.X - boxZoomW - 6, 3, boxZoomW, 20);
        _cursorBtnRect = new Rect(_boxZoomBtnRect.X - cursorW - 6, 3, cursorW, 20);
        _triggerBtnRect = new Rect(_cursorBtnRect.X - trigW - 6, 3, trigW, 20);
        _layoutBtnRect = new Rect(_triggerBtnRect.X - layoutW - 6, 3, layoutW, 20);
        _modeBtnRect = new Rect(_layoutBtnRect.X - modeW - 6, 3, modeW, 20);
    }

    private void RenderTimePositionMarker(DrawingContext context, double w, double headerH, double h, double marginL, double plotW)
    {
        double centerX = marginL + plotW / 2.0;
        double divX = plotW / 10.0;
        double markerX = centerX + TimeOffsetDivs * divX;

        if (markerX >= marginL && markerX <= marginL + plotW)
        {
            var linePen = new Pen(new SolidColorBrush(Color.FromArgb(70, 245, 158, 11)), 1.0)
            {
                DashStyle = DashStyle.Dash
            };
            context.DrawLine(linePen, new Point(markerX, headerH), new Point(markerX, h));

            _timePositionMarkerRect = new Rect(markerX - 8, headerH, 16, 12);
            var triGeom = new StreamGeometry();
            using (var ctx = triGeom.Open())
            {
                ctx.BeginFigure(new Point(markerX - 5, headerH + 1), isFilled: true);
                ctx.LineTo(new Point(markerX + 5, headerH + 1));
                ctx.LineTo(new Point(markerX, headerH + 7));
                ctx.EndFigure(isClosed: true);
            }
            context.DrawGeometry(new SolidColorBrush(Color.FromRgb(245, 158, 11)), null, triGeom);

            if (Math.Abs(TimeOffsetDivs) > 0.05 || Math.Abs(TimebaseZoom - 1.0) > 0.05)
            {
                string posStr = $"T: {(TimeOffsetDivs >= 0 ? "+" : "")}{TimeOffsetDivs:F2}d ({TimebaseZoom:F1}x)";
                var ft = new FormattedText(
                    posStr,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
                    8.5,
                    new SolidColorBrush(Color.FromRgb(245, 158, 11))
                );
                double rx = Math.Clamp(markerX - ft.Width / 2.0, marginL + 2, marginL + plotW - ft.Width - 4);
                context.DrawText(ft, new Point(rx, headerH + 8));
            }
        }
        else
        {
            _timePositionMarkerRect = default;
        }
    }

    private void RenderOverlayTimeDomain(DrawingContext context, double w, double h)
    {
        double headerH = 26.0;
        double screenH = h - headerH;
        var tokens = CircuitThemeService.Instance.Tokens;

        // 8 Horizontal divisions, 10 Vertical divisions
        var gridPen = new Pen(tokens.ScopeGridBrush, 1);
        for (int i = 1; i < 8; i++)
        {
            double y = headerH + (screenH / 8.0) * i;
            context.DrawLine(gridPen, new Point(0, y), new Point(w, y));
        }

        for (int i = 1; i < 10; i++)
        {
            double x = (w / 10.0) * i;
            context.DrawLine(gridPen, new Point(x, headerH), new Point(x, h));
        }

        // Center axes lines
        double centerY = headerH + screenH / 2.0;
        double centerX = w / 2.0;
        var centerPen = new Pen(tokens.ScopeCenterBrush, 1);
        context.DrawLine(centerPen, new Point(0, centerY), new Point(w, centerY));
        context.DrawLine(centerPen, new Point(centerX, headerH), new Point(centerX, h));

        // Center crosshair tick marks
        var tickPen = new Pen(tokens.ScopeTickBrush, 1);
        for (int i = 0; i <= 50; i++)
        {
            double tx = (w / 50.0) * i;
            context.DrawLine(tickPen, new Point(tx, centerY - 2), new Point(tx, centerY + 2));
        }
        for (int i = 0; i <= 40; i++)
        {
            double ty = headerH + (screenH / 40.0) * i;
            context.DrawLine(tickPen, new Point(centerX - 2, ty), new Point(centerX + 2, ty));
        }

        double marginL = 36.0;
        double marginR = 36.0;
        double plotW = w - marginL - marginR;
        double divY = screenH / 8.0;
        double divX = plotW / 10.0;
        double panPx = TimeOffsetDivs * divX;
        double plotCenterX = marginL + plotW / 2.0;

        _groundTabRects.Clear();

        // 1. Draw Traces inside clipped viewport
        using (context.PushClip(new Rect(marginL, headerH, plotW, screenH)))
        {
            foreach (var trace in Channels)
            {
                if (!trace.IsVisible || trace.Samples.Count < 2) continue;

                var samplesList = trace.Samples.ToArray();
                int count = samplesList.Length;

                double vPerDiv = trace.VoltsPerDiv > 1e-9 ? trace.VoltsPerDiv : 1.0;
                double scaleY = divY / vPerDiv;
                double offsetPx = trace.VerticalOffsetDivs * divY;

                Point? lastPoint = null;
                var pathGeom = new StreamGeometry();
                using (var ctx = pathGeom.Open())
                {
                    double norm0 = (count > 1 ? (0.0 / (count - 1)) : 0.0) - 0.5;
                    double startX = plotCenterX + (norm0 * TimebaseZoom * plotW) + panPx;
                    double startY = centerY - offsetPx - samplesList[0] * scaleY;
                    ctx.BeginFigure(new Point(startX, Math.Clamp(startY, headerH - 50, h + 50)), isFilled: false);

                    for (int i = 1; i < count; i++)
                    {
                        double norm = ((double)i / (count - 1)) - 0.5;
                        double x = plotCenterX + (norm * TimebaseZoom * plotW) + panPx;
                        double y = centerY - offsetPx - samplesList[i] * scaleY;
                        var pt = new Point(x, Math.Clamp(y, headerH - 50, h + 50));
                        ctx.LineTo(pt);
                        if (i == count - 1) lastPoint = pt;
                    }
                }

                var glowPen = new Pen(new SolidColorBrush(Color.FromArgb(60, trace.Color.R, trace.Color.G, trace.Color.B)), 4.0)
                {
                    LineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };
                var corePen = new Pen(new SolidColorBrush(trace.Color), 1.9)
                {
                    LineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };

                context.DrawGeometry(null, glowPen, pathGeom);
                context.DrawGeometry(null, corePen, pathGeom);

                if (lastPoint.HasValue && lastPoint.Value.X >= marginL && lastPoint.Value.X <= marginL + plotW)
                {
                    context.DrawEllipse(new SolidColorBrush(Color.FromArgb(90, trace.Color.R, trace.Color.G, trace.Color.B)), null, lastPoint.Value, 5.0, 5.0);
                    context.DrawEllipse(new SolidColorBrush(trace.Color), null, lastPoint.Value, 2.8, 2.8);
                }
            }
        }

        // 2. Draw Ground Indicator Flags [1▶]..[4▶] on left bezel
        foreach (var trace in Channels)
        {
            if (!trace.IsVisible) continue;
            double offsetPx = trace.VerticalOffsetDivs * divY;
            double gndY = Math.Clamp(centerY - offsetPx, headerH + 10, h - 10);
            DrawGroundFlag(context, trace.ChannelIndex, trace.Color, gndY, marginL);
        }

        // 3. Render Horizontal Time Position Marker [▼] at top of graticule
        RenderTimePositionMarker(context, w, headerH, h, marginL, plotW);

        // 4. Draw Trigger Level Line and Marker [T▶] on right bezel
        RenderTriggerMarker(context, w, h, headerH, centerY, screenH, marginR);

        // 5. Render Left Voltage Scale for the active channel
        RenderLeftVoltageScale(context, marginL, headerH, h, centerY);

        // 4. Interactive Measurement Cursors
        if (ShowCursors)
        {
            RenderMeasurementCursorLines(context, w, h, headerH);
        }

        // 5. Header Banner & Channel Badges
        RenderHeaderBannerAndBadges(context, w, headerH);

        // 6. Live Waveform Stats Ribbon
        if (!ShowCursors)
        {
            RenderStatsRibbon(context, w, h);
        }
        else
        {
            RenderMeasurementCursorHandles(context, w, h, headerH);
        }
    }

    private void RenderStackedTimeDomain(DrawingContext context, double w, double h)
    {
        double headerH = 26.0;
        double screenH = h - headerH;
        var activeChannels = Channels.Where(c => c.IsVisible).ToList();
        if (activeChannels.Count == 0) activeChannels = Channels.Take(2).ToList();

        int numPanes = activeChannels.Count;
        double paneH = screenH / numPanes;
        double marginL = 36.0;
        double marginR = 24.0;
        double plotW = w - marginL - marginR;
        double divX = plotW / 10.0;
        double panPx = TimeOffsetDivs * divX;
        double plotCenterX = marginL + plotW / 2.0;

        var tokens = CircuitThemeService.Instance.Tokens;
        var gridPen = new Pen(tokens.ScopeGridBrush, 0.8);
        var dividerPen = new Pen(new SolidColorBrush(Color.FromArgb(90, 40, 60, 85)), 1.2);

        _groundTabRects.Clear();

        for (int k = 0; k < numPanes; k++)
        {
            var trace = activeChannels[k];
            double paneTop = headerH + k * paneH;
            double paneCenterY = paneTop + paneH / 2.0;
            double offsetPx = trace.VerticalOffsetDivs * (paneH / 4.0);

            // Pane background separation
            if (k % 2 == 1)
            {
                context.FillRectangle(new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)), new Rect(0, paneTop, w, paneH));
            }

            // Divider between panes
            if (k > 0)
            {
                context.DrawLine(dividerPen, new Point(0, paneTop), new Point(w, paneTop));
            }

            // Sub-grid lines within pane
            for (int gi = 1; gi <= 3; gi++)
            {
                double gy = paneTop + (paneH / 4.0) * gi;
                context.DrawLine(gridPen, new Point(0, gy), new Point(w, gy));
            }
            var centerSubPen = new Pen(tokens.ScopeCenterBrush, 0.8);
            context.DrawLine(centerSubPen, new Point(0, paneCenterY), new Point(w, paneCenterY));

            // Ground marker for this channel at its ground level
            double gndY = Math.Clamp(paneCenterY - offsetPx, paneTop + 8, paneTop + paneH - 8);
            DrawGroundFlag(context, trace.ChannelIndex, trace.Color, gndY, marginL);

            // Channel pane title & scale readout
            string vposStr = Math.Abs(trace.VerticalOffsetDivs) > 0.05 ? $" (Offset: {(trace.VerticalOffsetDivs >= 0 ? "+" : "")}{trace.VerticalOffsetDivs:F1}d)" : "";
            var ftTitle = new FormattedText(
                $"{trace.Name}: {FormatTraceValue(trace.CurrentValue, trace.Unit)}{vposStr}",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
                9.5,
                new SolidColorBrush(trace.Color)
            );
            context.DrawText(ftTitle, new Point(marginL + 8, paneTop + 4));

            // Draw trace in pane with clip
            if (trace.Samples.Count >= 2)
            {
                using (context.PushClip(new Rect(marginL, paneTop, plotW, paneH)))
                {
                    var samplesList = trace.Samples.ToArray();
                    int count = samplesList.Length;
                    double paneDivY = paneH / 4.0;
                    double vPerDiv = trace.VoltsPerDiv > 1e-9 ? trace.VoltsPerDiv : 1.0;
                    double scaleY = paneDivY / vPerDiv;

                    var pathGeom = new StreamGeometry();
                    using (var ctx = pathGeom.Open())
                    {
                        double norm0 = (count > 1 ? (0.0 / (count - 1)) : 0.0) - 0.5;
                        double startX = plotCenterX + (norm0 * TimebaseZoom * plotW) + panPx;
                        double startY = paneCenterY - offsetPx - samplesList[0] * scaleY;
                        ctx.BeginFigure(new Point(startX, Math.Clamp(startY, paneTop - 50, paneTop + paneH + 50)), isFilled: false);

                        for (int i = 1; i < count; i++)
                        {
                            double norm = ((double)i / (count - 1)) - 0.5;
                            double x = plotCenterX + (norm * TimebaseZoom * plotW) + panPx;
                            double y = paneCenterY - offsetPx - samplesList[i] * scaleY;
                            ctx.LineTo(new Point(x, Math.Clamp(y, paneTop - 50, paneTop + paneH + 50)));
                        }
                    }

                    var glowPen = new Pen(new SolidColorBrush(Color.FromArgb(60, trace.Color.R, trace.Color.G, trace.Color.B)), 3.5);
                    var corePen = new Pen(new SolidColorBrush(trace.Color), 1.7);
                    context.DrawGeometry(null, glowPen, pathGeom);
                    context.DrawGeometry(null, corePen, pathGeom);
                }
            }
        }

        // Render Horizontal Time Position Marker [▼] at top of graticule
        RenderTimePositionMarker(context, w, headerH, h, marginL, plotW);

        // Header Banner & Badges
        RenderHeaderBannerAndBadges(context, w, headerH);
    }

    private void RenderXyDisplay(DrawingContext context, double w, double h)
    {
        double headerH = 26.0;
        double screenH = h - headerH;
        double centerY = headerH + screenH / 2.0;
        double centerX = w / 2.0;

        double divX = (w - 72.0) / 10.0;
        double panX = TimeOffsetDivs * divX;
        double panY = (Channels.Count > 1 ? Channels[1].VerticalOffsetDivs : 0.0) * (screenH / 8.0);
        double effCenterX = centerX + panX;
        double effCenterY = centerY - panY;

        var tokens = CircuitThemeService.Instance.Tokens;
        var centerPen = new Pen(tokens.ScopeCenterBrush, 1);
        context.DrawLine(centerPen, new Point(0, effCenterY), new Point(w, effCenterY));
        context.DrawLine(centerPen, new Point(effCenterX, headerH), new Point(effCenterX, h));

        // Circular graticule rings for polar / XY view
        var ringPen = new Pen(tokens.ScopeGridBrush, 0.8);
        double maxRadius = Math.Min(centerX - 40, screenH / 2.0 - 10) * TimebaseZoom;
        for (int r = 1; r <= 4; r++)
        {
            double rad = (maxRadius / 4.0) * r;
            context.DrawEllipse(null, ringPen, new Point(effCenterX, effCenterY), rad, rad);
        }

        var chX = Channels.FirstOrDefault(c => c.ChannelIndex == 1) ?? Channels.FirstOrDefault();
        var chY = Channels.FirstOrDefault(c => c.ChannelIndex == 2) ?? Channels.Skip(1).FirstOrDefault();

        if (chX != null && chY != null && chX.Samples.Count >= 2 && chY.Samples.Count >= 2)
        {
            var arrX = chX.Samples.ToArray();
            var arrY = chY.Samples.ToArray();
            int minLen = Math.Min(arrX.Length, arrY.Length);

            double maxX = GetEngineeringNiceMax(arrX.Max(s => Math.Abs(s)));
            double maxY = GetEngineeringNiceMax(arrY.Max(s => Math.Abs(s)));

            double scaleX = (maxRadius * 0.95) / Math.Max(1e-12, maxX);
            double scaleY = (maxRadius * 0.95) / Math.Max(1e-12, maxY);

            using (context.PushClip(new Rect(0, headerH, w, screenH)))
            {
                var pathGeom = new StreamGeometry();
                using (var ctx = pathGeom.Open())
                {
                    double sX = effCenterX + arrX[0] * scaleX;
                    double sY = effCenterY - arrY[0] * scaleY;
                    ctx.BeginFigure(new Point(sX, sY), isFilled: false);

                    for (int i = 1; i < minLen; i++)
                    {
                        double x = effCenterX + arrX[i] * scaleX;
                        double y = effCenterY - arrY[i] * scaleY;
                        ctx.LineTo(new Point(x, y));
                    }
                }

                var neonColor = Color.FromRgb(6, 182, 212);
                var glowPen = new Pen(new SolidColorBrush(Color.FromArgb(70, neonColor.R, neonColor.G, neonColor.B)), 4.0);
                var corePen = new Pen(new SolidColorBrush(neonColor), 2.0);
                context.DrawGeometry(null, glowPen, pathGeom);
                context.DrawGeometry(null, corePen, pathGeom);
            }
        }

        var ftInfo = new FormattedText(
            $"XY Lissajous: X = {chX?.Name ?? "CH1"}, Y = {chY?.Name ?? "CH2"}",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
            10,
            new SolidColorBrush(Color.FromRgb(148, 163, 184))
        );
        context.DrawText(ftInfo, new Point(12, h - 22));

        RenderHeaderBannerAndBadges(context, w, headerH);
    }

    private void DrawGroundFlag(DrawingContext context, int channelIndex, Color color, double y, double marginL)
    {
        double tabW = 20.0;
        double tabH = 14.0;
        double tabX = marginL - tabW - 4;
        double tabY = y - tabH / 2.0;

        var tabRect = new Rect(tabX, tabY, tabW, tabH);
        _groundTabRects[channelIndex] = tabRect;

        // Pill shape with pointer arrow
        context.FillRectangle(new SolidColorBrush(color), tabRect, 3);

        var ft = new FormattedText(
            $"{channelIndex}▶",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            8.5,
            Brushes.Black
        );
        context.DrawText(ft, new Point(tabX + 2, tabY + 1));
    }

    private void RenderTriggerMarker(DrawingContext context, double w, double h, double headerH, double centerY, double screenH, double marginR)
    {
        var trigCh = Channels.FirstOrDefault(c => c.ChannelIndex == TriggerSettings.SourceChannelIndex) ?? Channels.FirstOrDefault();
        if (trigCh == null || trigCh.Samples.Count < 2) return;

        double divY = screenH / 8.0;
        double vPerDiv = trigCh.VoltsPerDiv > 1e-9 ? trigCh.VoltsPerDiv : 1.0;
        double scaleY = divY / vPerDiv;
        double offsetPx = trigCh.VerticalOffsetDivs * divY;
        double trigY = Math.Clamp(centerY - offsetPx - TriggerSettings.Level * scaleY, headerH + 6, h - 6);

        // Dashed trigger level line across screen
        var trigLinePen = new Pen(new SolidColorBrush(Color.FromArgb(90, 245, 158, 11)), 1.0)
        {
            DashStyle = DashStyle.Dash
        };
        context.DrawLine(trigLinePen, new Point(36, trigY), new Point(w - marginR, trigY));

        // Draggable trigger flag on right bezel
        double tabW = 20.0;
        double tabH = 14.0;
        double tabX = w - marginR + 4;
        double tabY = trigY - tabH / 2.0;
        _triggerTabRect = new Rect(tabX, tabY, tabW, tabH);

        var trigBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
        context.FillRectangle(trigBrush, _triggerTabRect, 3);

        var ftT = new FormattedText(
            "◀T",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            8.5,
            Brushes.Black
        );
        context.DrawText(ftT, new Point(tabX + 2, tabY + 1));
    }

    private void RenderHeaderBannerAndBadges(DrawingContext context, double w, double headerH)
    {
        // Top HUD Header Banner
        var headerRect = new Rect(0, 0, w, headerH);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(235, 10, 16, 24)), headerRect);
        context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(80, 28, 44, 62)), 1), new Point(0, headerH), new Point(w, headerH));

        // Channel readout badges inside Top HUD Header
        _traceBadgeRects.Clear();
        double textX = 10.0;
        foreach (var trace in Channels)
        {
            if (trace.Samples.Count < 2) continue;

            var samplesList = trace.Samples.ToArray();
            double peakVal = samplesList.Max(s => Math.Abs(s));
            string curFormatted = FormatTraceValue(trace.CurrentValue, trace.Unit);
            string peakFormatted = FormatTraceValue(peakVal, trace.Unit);
            string readout = trace.IsVisible
                ? $"{trace.Name}: {curFormatted} (Pk: {peakFormatted})"
                : $"{trace.Name}: [MUTED]";

            var badgeColor = trace.IsVisible ? trace.Color : Color.FromArgb(120, trace.Color.R, trace.Color.G, trace.Color.B);
            var labelText = new FormattedText(
                readout,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
                9.5,
                new SolidColorBrush(badgeColor)
            );

            var badgeRect = new Rect(textX - 3, 3, labelText.Width + 6, 20);
            _traceBadgeRects.Add(badgeRect);

            var bgBrush = trace.IsVisible
                ? new SolidColorBrush(Color.FromArgb(200, 16, 24, 34))
                : new SolidColorBrush(Color.FromArgb(140, 10, 14, 20));
            var borderBrush = new SolidColorBrush(Color.FromArgb(50, 51, 65, 85));

            context.FillRectangle(bgBrush, badgeRect, 4);
            context.DrawRectangle(new Pen(borderBrush, 1), badgeRect, 4);

            context.DrawText(labelText, new Point(textX, 6));
            textX += labelText.Width + 10;
        }

        // Right-aligned acquisition & trigger telemetry status pill (if width allows)
        double statusW = 200.0;
        if (textX + 20 < w - statusW)
        {
            string timeStr = TimebaseZoom != 1.0 ? $"TIME: {TimebaseMs * 10.0 / TimebaseZoom:F1}ms ({TimebaseZoom:F1}x)" : $"TIME: {TimebaseMs * 10.0:F1}ms";
            var ftStatus = new FormattedText(
                $"{timeStr}  •  TRIG: {TriggerSettings.Mode.ToString().ToUpperInvariant()}",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
                9.0,
                new SolidColorBrush(Color.FromArgb(160, 148, 163, 184))
            );
            context.DrawText(ftStatus, new Point(w - ftStatus.Width - 12, 6));
        }
    }

    private void RenderLeftVoltageScale(DrawingContext context, double marginL, double headerH, double h, double centerY)
    {
        var trace = Channels.FirstOrDefault(t => t.ChannelIndex == ActiveChannelIndex)
                    ?? Channels.FirstOrDefault(t => t.IsVisible && t.Samples.Count >= 2)
                    ?? Channels.FirstOrDefault(t => t.Samples.Count >= 2);
        if (trace == null) return;

        double screenH = h - headerH;
        double divY = screenH / 8.0;
        double vPerDiv = trace.VoltsPerDiv > 1e-9 ? trace.VoltsPerDiv : 1.0;
        double offsetPx = trace.VerticalOffsetDivs * divY;
        double effectiveCenterY = centerY - offsetPx;

        var marginBg = new SolidColorBrush(Color.FromArgb(140, 8, 12, 18));
        context.FillRectangle(marginBg, new Rect(0, headerH, marginL, h - headerH));
        context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(50, 40, 60, 85)), 1), new Point(marginL, headerH), new Point(marginL, h));

        // Draw ticks aligned to each vertical graticule division line (-4 to +4 from ground)
        for (int div = -4; div <= 4; div++)
        {
            double yPos = effectiveCenterY - (div * divY);
            if (yPos >= headerH + 2 && yPos <= h - 4)
            {
                double vLevel = div * vPerDiv;
                var tickPen = new Pen(new SolidColorBrush(Color.FromArgb(140, trace.Color.R, trace.Color.G, trace.Color.B)), 1.2);
                context.DrawLine(tickPen, new Point(marginL - 4, yPos), new Point(marginL, yPos));

                string vStr = FormatTraceValue(vLevel, trace.Unit);
                var ft = new FormattedText(
                    vStr,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
                    8.5,
                    new SolidColorBrush(Color.FromArgb(220, trace.Color.R, trace.Color.G, trace.Color.B))
                );
                double textY = Math.Clamp(yPos - ft.Height / 2.0, headerH + 1, h - ft.Height - 2);
                context.DrawText(ft, new Point(Math.Max(2, marginL - 6 - ft.Width), textY));
            }
        }
    }

    private void RenderStatsRibbon(DrawingContext context, double w, double h)
    {
        var trace = Channels.FirstOrDefault(t => t.Samples.Count >= 2 && t.IsVisible) ?? Channels.FirstOrDefault(t => t.Samples.Count >= 2);
        if (trace == null) return;

        var arr = trace.Samples.ToArray();
        double max = arr.Max();
        double min = arr.Min();
        double range = max - min;
        double sumSq = arr.Sum(s => s * s);
        double rms = Math.Sqrt(sumSq / arr.Length);

        double mid = (max + min) / 2.0;
        double freq = 0.0;
        if (range > 0.01)
        {
            var crossings = new List<int>();
            for (int i = 0; i < arr.Length - 1; i++)
            {
                if (arr[i] <= mid && arr[i + 1] > mid) crossings.Add(i);
            }

            if (crossings.Count >= 2)
            {
                double totalSamplesSpan = crossings.Last() - crossings.First();
                double avgPeriodSamples = totalSamplesSpan / (crossings.Count - 1);
                double totalTimeMs = TimebaseMs * 10.0;
                double periodMs = avgPeriodSamples * (totalTimeMs / arr.Length);
                if (periodMs > 1e-4) freq = 1000.0 / periodMs;
            }
        }

        string maxStr = FormatTraceValue(max, trace.Unit);
        string minStr = FormatTraceValue(min, trace.Unit);
        string rangeStr = FormatTraceValue(range, trace.Unit);
        string rmsStr = FormatTraceValue(rms, trace.Unit);
        string freqStr = freq > 1000.0 ? $"{freq / 1000.0:F2} kHz" : $"{freq:F2} Hz";

        var metricsList = new List<(string Label, string Value)>
        {
            ("MAX", maxStr),
            ("MIN", minStr),
            ("VPP", rangeStr),
            ("FREQ", freqStr),
            ("RMS", rmsStr)
        };

        if (Math.Abs(TimeOffsetDivs) > 0.05 || Math.Abs(TimebaseZoom - 1.0) > 0.05)
        {
            metricsList.Add(("POS", $"{(TimeOffsetDivs >= 0 ? "+" : "")}{TimeOffsetDivs:F1}d"));
            metricsList.Add(("ZOOM", $"{TimebaseZoom:F1}x"));
        }
        if (Channels.Any(c => Math.Abs(c.VerticalOffsetDivs) > 0.05))
        {
            var activeCh = Channels.FirstOrDefault(c => c.ChannelIndex == ActiveChannelIndex) ?? trace;
            metricsList.Add(("VPOS", $"{(activeCh.VerticalOffsetDivs >= 0 ? "+" : "")}{activeCh.VerticalOffsetDivs:F1}d"));
        }

        (string Label, string Value)[] metrics = metricsList.ToArray();

        double boxH = 20.0;
        double boxY = h - boxH - 5;
        double curX = 12.0;

        double totalW = 20.0;
        var formattedSegments = new List<(FormattedText LabelFt, FormattedText ValFt, double SegW)>();

        foreach (var (lbl, val) in metrics)
        {
            var lFt = new FormattedText(
                lbl,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
                8.0,
                new SolidColorBrush(Color.FromRgb(0, 240, 255))
            );
            var vFt = new FormattedText(
                val,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas,SF Mono,Menlo,monospace", FontStyle.Normal, FontWeight.Bold),
                9.5,
                Brushes.White
            );
            double segW = lFt.Width + 4 + vFt.Width + 12;
            formattedSegments.Add((lFt, vFt, segW));
            totalW += segW;
        }

        var ribbonRect = new Rect(curX, boxY, totalW, boxH);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(230, 9, 14, 22)), ribbonRect, 4);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromArgb(90, trace.Color.R, trace.Color.G, trace.Color.B)), 1), ribbonRect, 4);

        double dotR = 3.5;
        var dotCenter = new Point(curX + 8 + dotR, boxY + boxH / 2.0);
        context.DrawEllipse(new SolidColorBrush(trace.Color), null, dotCenter, dotR, dotR);

        double segX = curX + 8 + dotR * 2 + 8;
        for (int i = 0; i < formattedSegments.Count; i++)
        {
            var (lFt, vFt, segW) = formattedSegments[i];
            context.DrawText(lFt, new Point(segX, boxY + (boxH - lFt.Height) / 2.0));
            context.DrawText(vFt, new Point(segX + lFt.Width + 4, boxY + (boxH - vFt.Height) / 2.0));

            segX += segW;
            if (i < formattedSegments.Count - 1)
            {
                var sepPen = new Pen(new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)), 1);
                context.DrawLine(sepPen, new Point(segX - 6, boxY + 4), new Point(segX - 6, boxY + boxH - 4));
            }
        }
    }

    private void RenderMeasurementCursorLines(DrawingContext context, double w, double h, double headerH)
    {
        double x1 = Cursor1X * w;
        double x2 = Cursor2X * w;

        var c1Pen = new Pen(new SolidColorBrush(Color.FromRgb(245, 158, 11)), 1.5) { DashStyle = DashStyle.Dash };
        var c2Pen = new Pen(new SolidColorBrush(Color.FromRgb(6, 182, 212)), 1.5) { DashStyle = DashStyle.Dash };

        context.DrawLine(c1Pen, new Point(x1, headerH), new Point(x1, h));
        context.DrawLine(c2Pen, new Point(x2, headerH), new Point(x2, h));

        double totalTimeMs = TimebaseMs * 10.0;
        double t1 = Cursor1X * totalTimeMs;
        double t2 = Cursor2X * totalTimeMs;
        double dt = Math.Abs(t2 - t1);
        double f = dt > 1e-4 ? (1000.0 / dt) : 0.0;

        double v1 = 0.0, v2 = 0.0;
        if (Channels.Count > 0 && Channels[0].Samples.Count > 1)
        {
            var arr = Channels[0].Samples.ToArray();
            int idx1 = Math.Clamp((int)(Cursor1X * (arr.Length - 1)), 0, arr.Length - 1);
            int idx2 = Math.Clamp((int)(Cursor2X * (arr.Length - 1)), 0, arr.Length - 1);
            v1 = arr[idx1];
            v2 = arr[idx2];
        }
        double dv = Math.Abs(v2 - v1);

        string fStr = f > 1000.0 ? $"{f / 1000.0:F2} kHz" : $"{f:F1} Hz";
        string ch1Label = Channels.Count > 0 && Channels[0].Unit.Equals("A", StringComparison.OrdinalIgnoreCase) ? "ΔI1" : "ΔV1";
        string dv1Str = FormatTraceValue(dv, Channels.Count > 0 ? Channels[0].Unit : "V");
        string cursorInfo = $"X1: {t1:F2}ms | X2: {t2:F2}ms | ΔX: {dt:F2}ms ({fStr}) | {ch1Label}: {dv1Str}";

        double dumbbellY = h - 42.0;
        var barPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), 1.5);
        double leftX = Math.Min(x1, x2);
        double rightX = Math.Max(x1, x2);
        context.DrawLine(barPen, new Point(leftX, dumbbellY), new Point(rightX, dumbbellY));

        context.DrawEllipse(new SolidColorBrush(Colors.White), null, new Point(leftX, dumbbellY), 4.0, 4.0);
        context.DrawEllipse(new SolidColorBrush(Colors.White), null, new Point(rightX, dumbbellY), 4.0, 4.0);

        string dtLabel = dt >= 1.0 ? $"{dt:F1} ms Δ" : $"{dt * 1000.0:F0} µs Δ";
        var ftDt = new FormattedText(
            dtLabel,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            9.5,
            Brushes.White
        );
        double pillW = ftDt.Width + 14;
        double pillH = 16.0;
        double pillX = (leftX + rightX) / 2.0 - pillW / 2.0;
        double pillY = dumbbellY - pillH / 2.0;
        var pillRect = new Rect(pillX, pillY, pillW, pillH);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(235, 12, 18, 26)), pillRect, 8);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromArgb(70, 51, 65, 85)), 1), pillRect, 8);
        context.DrawText(ftDt, new Point(pillX + 7, pillY + 1.5));

        var ft = new FormattedText(
            cursorInfo,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
            10.0,
            new SolidColorBrush(Color.FromRgb(226, 232, 240))
        );

        var box = new Rect(w / 2.0 - ft.Width / 2.0 - 8, h - 22, ft.Width + 16, 18);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(235, 12, 18, 26)), box, 4);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromArgb(60, 51, 65, 85)), 1), box, 4);
        context.DrawText(ft, new Point(w / 2.0 - ft.Width / 2.0, h - 20));
    }

    private void RenderMeasurementCursorHandles(DrawingContext context, double w, double h, double headerH)
    {
        double x1 = Cursor1X * w;
        double x2 = Cursor2X * w;

        var c1Color = Color.FromRgb(245, 158, 11);
        var c2Color = Color.FromRgb(6, 182, 212);

        DrawCursorHandle(context, x1, headerH + 2, "X1", c1Color);
        DrawCursorHandle(context, x2, headerH + 2, "X2", c2Color);
    }

    private void DrawCursorHandle(DrawingContext context, double x, double y, string label, Color color)
    {
        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            ctx.BeginFigure(new Point(x - 9, y), isFilled: true);
            ctx.LineTo(new Point(x + 9, y));
            ctx.LineTo(new Point(x + 9, y + 10));
            ctx.LineTo(new Point(x, y + 17));
            ctx.LineTo(new Point(x - 9, y + 10));
            ctx.EndFigure(isClosed: true);
        }

        context.DrawGeometry(new SolidColorBrush(color), new Pen(new SolidColorBrush(Colors.White), 1), geom);

        var ft = new FormattedText(
            label,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            8.0,
            Brushes.Black
        );
        context.DrawText(ft, new Point(x - ft.Width / 2.0, y + 1.5));
    }

    private void RenderFftSpectrum(DrawingContext context, double w, double h)
    {
        double headerH = 26.0;
        double screenH = h - headerH;
        var tokens = CircuitThemeService.Instance.Tokens;

        var gridPen = new Pen(tokens.ScopeGridBrush, 1);
        for (int i = 1; i < 8; i++)
        {
            double y = headerH + (screenH / 8.0) * i;
            context.DrawLine(gridPen, new Point(0, y), new Point(w, y));
        }
        for (int i = 1; i < 10; i++)
        {
            double x = (w / 10.0) * i;
            context.DrawLine(gridPen, new Point(x, headerH), new Point(x, h));
        }

        double sampleRate = 1000.0 / Math.Max(0.1, TimebaseMs / 20.0);
        double topPeakFreq = 0.0;
        double topPeakDb = -120.0;
        Color topPeakColor = Colors.White;

        foreach (var trace in Channels)
        {
            if (!trace.IsVisible || trace.Samples.Count < 16) continue;

            var (freqs, magsDb, peakFreq, peakDb) = ComputeFft(trace.Samples.ToArray(), sampleRate);
            if (freqs.Length < 2) continue;

            if (peakDb > topPeakDb)
            {
                topPeakDb = peakDb;
                topPeakFreq = peakFreq;
                topPeakColor = trace.Color;
            }

            double minDb = -80.0;
            double maxDb = 20.0;
            double dbRange = maxDb - minDb;

            double dx = w / (freqs.Length - 1);
            var pathGeom = new StreamGeometry();
            using (var ctx = pathGeom.Open())
            {
                double startY = headerH + screenH * (1.0 - (Math.Clamp(magsDb[0], minDb, maxDb) - minDb) / dbRange);
                ctx.BeginFigure(new Point(0, startY), isFilled: false);

                for (int i = 1; i < freqs.Length; i++)
                {
                    double x = i * dx;
                    double y = headerH + screenH * (1.0 - (Math.Clamp(magsDb[i], minDb, maxDb) - minDb) / dbRange);
                    ctx.LineTo(new Point(x, y));
                }
            }

            var glowPen = new Pen(new SolidColorBrush(Color.FromArgb(60, trace.Color.R, trace.Color.G, trace.Color.B)), 3.5);
            var corePen = new Pen(new SolidColorBrush(trace.Color), 1.8);
            context.DrawGeometry(null, glowPen, pathGeom);
            context.DrawGeometry(null, corePen, pathGeom);
        }

        RenderHeaderBannerAndBadges(context, w, headerH);
    }

    private void RenderInstrumentButtons(DrawingContext context, double w, double h)
    {
        // 1. Mode Button
        string modeLabel = DisplayLayout switch
        {
            OscilloscopeDisplayLayout.FftSpectrum => "MODE: FFT",
            OscilloscopeDisplayLayout.XY => "MODE: XY",
            _ => "MODE: TIME"
        };
        DrawToolButton(context, _modeBtnRect, modeLabel, DisplayLayout != OscilloscopeDisplayLayout.Overlay && DisplayLayout != OscilloscopeDisplayLayout.Stacked);

        // 2. Layout Button
        string layoutLabel = DisplayLayout == OscilloscopeDisplayLayout.Stacked ? "LAYOUT: STACKED" : "LAYOUT: OVERLAY";
        DrawToolButton(context, _layoutBtnRect, layoutLabel, DisplayLayout == OscilloscopeDisplayLayout.Stacked);

        // 3. Trigger Mode Button
        string trigLabel = $"TRIG: {TriggerSettings.Mode.ToString().ToUpperInvariant()}";
        DrawToolButton(context, _triggerBtnRect, trigLabel, TriggerSettings.Mode != TriggerMode.Auto);

        // 4. Cursors Toggle Button
        string cursorLabel = ShowCursors ? "CURSORS: ON" : "CURSORS: OFF";
        DrawToolButton(context, _cursorBtnRect, cursorLabel, ShowCursors);

        // 5. Box Zoom Button
        string boxLabel = IsBoxZoomMode ? "BOX: ACTIVE" : "BOX ZOOM";
        DrawToolButton(context, _boxZoomBtnRect, boxLabel, IsBoxZoomMode);

        // 6. Center Pan Button
        bool isPanned = Math.Abs(TimeOffsetDivs) > 0.01 || Math.Abs(TimebaseZoom - 1.0) > 0.01 || Channels.Any(c => Math.Abs(c.VerticalOffsetDivs) > 0.01);
        DrawToolButton(context, _centerPanBtnRect, "CENTER PAN", isPanned);

        // 7. AutoSet Button
        DrawToolButton(context, _autoSetBtnRect, "AUTOSET", false);
    }

    private void DrawToolButton(DrawingContext context, Rect rect, string label, bool isActive)
    {
        var ft = new FormattedText(
            label,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
            9.5,
            new SolidColorBrush(isActive ? Color.FromRgb(56, 189, 248) : Color.FromRgb(148, 163, 184))
        );

        var bg = isActive ? Color.FromArgb(220, 30, 41, 59) : Color.FromArgb(200, 15, 23, 42);
        context.FillRectangle(new SolidColorBrush(bg), rect, 4);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromArgb(70, 51, 65, 85)), 1), rect, 4);
        context.DrawText(ft, new Point(rect.X + (rect.Width - ft.Width) / 2.0, rect.Y + (rect.Height - ft.Height) / 2.0));
    }

    public static double GetEngineeringNiceMax(double val)
    {
        if (val <= 1e-12) return 1.0;
        double exponent = Math.Floor(Math.Log10(val));
        double fraction = val / Math.Pow(10, exponent);
        double niceFraction = fraction switch
        {
            <= 1.0 => 1.0,
            <= 1.25 => 1.25,
            <= 2.0 => 2.0,
            <= 2.5 => 2.5,
            <= 5.0 => 5.0,
            _ => 10.0
        };
        return niceFraction * Math.Pow(10, exponent);
    }

    public static (double[] Frequencies, double[] MagnitudesDb, double PeakFreq, double PeakDb) ComputeFft(
        double[] samples, double sampleRateHz)
    {
        int n = 256;
        double[] real = new double[n];
        double[] imag = new double[n];

        int srcLen = samples.Length;
        int copyCount = Math.Min(n, srcLen);
        int srcOffset = Math.Max(0, srcLen - n);

        for (int i = 0; i < copyCount; i++)
        {
            double hann = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (n - 1)));
            real[i] = samples[srcOffset + i] * hann;
            imag[i] = 0.0;
        }

        int j = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imag[i], imag[j]) = (imag[j], imag[i]);
            }
            int k = n >> 1;
            while (k <= j)
            {
                j -= k;
                k >>= 1;
            }
            j += k;
        }

        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = -2.0 * Math.PI / len;
            double wstepR = Math.Cos(angle);
            double wstepI = Math.Sin(angle);

            for (int i = 0; i < n; i += len)
            {
                double wR = 1.0;
                double wI = 0.0;
                int half = len >> 1;

                for (int m = 0; m < half; m++)
                {
                    int u = i + m;
                    int v = i + m + half;

                    double tR = wR * real[v] - wI * imag[v];
                    double tI = wR * imag[v] + wI * real[v];

                    real[v] = real[u] - tR;
                    imag[v] = imag[u] - tI;
                    real[u] += tR;
                    imag[u] += tI;

                    double nextWR = wR * wstepR - wI * wstepI;
                    wI = wR * wstepI + wI * wstepR;
                    wR = nextWR;
                }
            }
        }

        int halfN = n / 2;
        double[] freqs = new double[halfN];
        double[] magsDb = new double[halfN];
        double peakDb = -120.0;
        double peakFreq = 0.0;

        double df = sampleRateHz / n;
        for (int k = 0; k < halfN; k++)
        {
            freqs[k] = k * df;
            double mag = Math.Sqrt(real[k] * real[k] + imag[k] * imag[k]) * (4.0 / n);
            double db = 20.0 * Math.Log10(Math.Max(1e-5, mag));
            magsDb[k] = db;

            if (k > 1 && db > peakDb)
            {
                peakDb = db;
                peakFreq = freqs[k];
            }
        }

        return (freqs, magsDb, peakFreq, peakDb);
    }

    private static string FormatTraceValue(double val, string unit)
    {
        if (double.IsNaN(val) || double.IsInfinity(val)) return $"0.00 {unit}";
        double abs = Math.Abs(val);
        if (abs > 1e9) return $"{(val > 0 ? ">1 G" : "<-1 G")}{unit}";
        if (unit.Equals("mA", StringComparison.OrdinalIgnoreCase))
        {
            return Device.FormatEngineering(val * 1e-3, "A");
        }
        if (unit.Equals("A", StringComparison.OrdinalIgnoreCase))
        {
            return Device.FormatEngineering(val, "A");
        }
        if (unit.Equals("V", StringComparison.OrdinalIgnoreCase))
        {
            return Device.FormatEngineering(val, "V");
        }
        return $"{val:F2} {unit}";
    }
}
