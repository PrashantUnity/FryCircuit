using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Controls;

public enum OscilloscopeDisplayMode
{
    TimeDomain,
    FftSpectrum
}

public class OscilloscopeTrace
{
    public string Name { get; set; } = "Trace";
    public Color Color { get; set; } = Color.FromRgb(0, 255, 136); // Neon green
    public Queue<double> Samples { get; } = new();
    public int MaxSamples { get; set; } = 300;
    public double CurrentValue => Samples.Count > 0 ? Samples.Last() : 0.0;
    public string Unit { get; set; } = "V";

    public void AddSample(double value)
    {
        Samples.Enqueue(value);
        while (Samples.Count > MaxSamples)
        {
            Samples.Dequeue();
        }
    }

    public void Clear()
    {
        Samples.Clear();
    }
}

public class OscilloscopeControl : Control
{
    public List<OscilloscopeTrace> Traces { get; } = new();

    public static readonly StyledProperty<double> TimebaseMsProperty =
        AvaloniaProperty.Register<OscilloscopeControl, double>(nameof(TimebaseMs), 10.0);

    public double TimebaseMs
    {
        get => GetValue(TimebaseMsProperty);
        set => SetValue(TimebaseMsProperty, value);
    }

    public OscilloscopeDisplayMode DisplayMode { get; set; } = OscilloscopeDisplayMode.TimeDomain;
    public bool ShowCursors { get; set; } = false;
    public double Cursor1X { get; set; } = 0.35; // Normalized 0..1
    public double Cursor2X { get; set; } = 0.65; // Normalized 0..1

    private int _draggingCursor = 0; // 0=none, 1=cursor1, 2=cursor2
    private Rect _modeBtnRect;
    private Rect _cursorBtnRect;

    public OscilloscopeControl()
    {
        ClipToBounds = true;
        MinHeight = 120;
        Focusable = true;

        // Default 2 channels: CH1 (Green/Gold), CH2 (Cyan)
        Traces.Add(new OscilloscopeTrace
        {
            Name = "CH1 (V)",
            Color = Color.FromRgb(0, 255, 136),
            Unit = "V"
        });

        Traces.Add(new OscilloscopeTrace
        {
            Name = "CH2 (I)",
            Color = Color.FromRgb(0, 220, 255),
            Unit = "mA"
        });
    }

    public void PushSamples(double ch1Value, double ch2Value)
    {
        if (Traces.Count > 0) Traces[0].AddSample(ch1Value);
        if (Traces.Count > 1) Traces[1].AddSample(ch2Value);
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pt = e.GetPosition(this);
        double w = Bounds.Width;

        if (_modeBtnRect.Contains(pt))
        {
            DisplayMode = (DisplayMode == OscilloscopeDisplayMode.TimeDomain)
                ? OscilloscopeDisplayMode.FftSpectrum
                : OscilloscopeDisplayMode.TimeDomain;
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

        if (ShowCursors && DisplayMode == OscilloscopeDisplayMode.TimeDomain)
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
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_draggingCursor != 0)
        {
            var pt = e.GetPosition(this);
            double ratio = Math.Clamp(pt.X / Math.Max(1.0, Bounds.Width), 0.02, 0.98);
            if (_draggingCursor == 1) Cursor1X = ratio;
            else if (_draggingCursor == 2) Cursor2X = ratio;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_draggingCursor != 0)
        {
            _draggingCursor = 0;
            e.Pointer.Capture(null);
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 10 || h <= 10) return;

        // Background: Deep dark CRT phosphor bezel
        var bgBrush = new SolidColorBrush(Color.FromRgb(8, 14, 20));
        context.FillRectangle(bgBrush, new Rect(0, 0, w, h));

        // Precompute instrument button bounds so layout is consistent
        ComputeInstrumentButtons(w);

        if (DisplayMode == OscilloscopeDisplayMode.FftSpectrum)
        {
            RenderFftSpectrum(context, w, h);
        }
        else
        {
            RenderTimeDomain(context, w, h);
        }

        // Draw Toolbar Buttons (Mode Toggle & Cursors Toggle)
        RenderInstrumentButtons(context, w, h);
    }

    private void ComputeInstrumentButtons(double w)
    {
        string modeLabel = DisplayMode == OscilloscopeDisplayMode.TimeDomain ? "MODE: TIME" : "MODE: FFT";
        var ftMode = new FormattedText(
            modeLabel,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            10,
            Brushes.White
        );

        string cursorLabel = ShowCursors ? "CURSORS: ON" : "CURSORS: OFF";
        var ftCursor = new FormattedText(
            cursorLabel,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            10,
            Brushes.White
        );

        double btnRightMargin = 12.0;
        double modeW = ftMode.Width + 14;
        double cursorW = ftCursor.Width + 14;

        _modeBtnRect = new Rect(w - modeW - btnRightMargin, 3, modeW, 20);
        _cursorBtnRect = new Rect(_modeBtnRect.X - cursorW - 8, 3, cursorW, 20);
    }

    private void RenderTimeDomain(DrawingContext context, double w, double h)
    {
        double headerH = 26.0;

        // Grid lines (8 horizontal divisions, 10 vertical divisions)
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 0, 230, 160)), 1);
        for (int i = 1; i < 8; i++)
        {
            double y = (h / 8.0) * i;
            context.DrawLine(gridPen, new Point(0, y), new Point(w, y));
        }

        for (int i = 1; i < 10; i++)
        {
            double x = (w / 10.0) * i;
            context.DrawLine(gridPen, new Point(x, 0), new Point(x, h));
        }

        // Center axes lines
        double centerY = h / 2.0;
        double centerX = w / 2.0;
        var centerPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 0, 255, 180)), 1);
        context.DrawLine(centerPen, new Point(0, centerY), new Point(w, centerY));
        context.DrawLine(centerPen, new Point(centerX, 0), new Point(centerX, h));

        // Center crosshair tick marks (minor divisions)
        var tickPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 0, 255, 180)), 1);
        for (int i = 0; i <= 50; i++)
        {
            double tx = (w / 50.0) * i;
            context.DrawLine(tickPen, new Point(tx, centerY - 2), new Point(tx, centerY + 2));
        }
        for (int i = 0; i <= 40; i++)
        {
            double ty = (h / 40.0) * i;
            context.DrawLine(tickPen, new Point(centerX - 2, ty), new Point(centerX + 2, ty));
        }

        double marginL = 36.0;

        // Draw Traces
        Point? lastPoint = null;
        foreach (var trace in Traces)
        {
            if (trace.Samples.Count < 2) continue;

            var samplesList = trace.Samples.ToArray();
            int count = samplesList.Length;
            double plotW = w - marginL - 12.0;
            double dx = count > 1 ? (plotW / (count - 1)) : 0;

            // Compute auto-scaling based on trace max amplitude
            double peakVal = samplesList.Max(s => Math.Abs(s));
            double scalePeak = Math.Max(1e-12, peakVal);
            double scaleY = (h * 0.38) / scalePeak;

            var pathGeom = new StreamGeometry();
            using (var ctx = pathGeom.Open())
            {
                double startX = marginL;
                double startY = centerY - samplesList[0] * scaleY;
                ctx.BeginFigure(new Point(startX, Math.Clamp(startY, headerH + 2, h - 3)), isFilled: false);

                for (int i = 1; i < count; i++)
                {
                    double x = startX + i * dx;
                    double y = centerY - samplesList[i] * scaleY;
                    var pt = new Point(x, Math.Clamp(y, headerH + 2, h - 3));
                    ctx.LineTo(pt);
                    if (i == count - 1) lastPoint = pt;
                }
            }

            // Double pass: soft luminous glow + sharp trace core
            var glowPen = new Pen(new SolidColorBrush(Color.FromArgb(65, trace.Color.R, trace.Color.G, trace.Color.B)), 4.5)
            {
                LineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            var corePen = new Pen(new SolidColorBrush(trace.Color), 2.0)
            {
                LineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };

            context.DrawGeometry(null, glowPen, pathGeom);
            context.DrawGeometry(null, corePen, pathGeom);

            // Glowing real-time sweep terminal dot
            if (lastPoint.HasValue)
            {
                context.DrawEllipse(new SolidColorBrush(Color.FromArgb(90, trace.Color.R, trace.Color.G, trace.Color.B)), null, lastPoint.Value, 5.5, 5.5);
                context.DrawEllipse(new SolidColorBrush(trace.Color), null, lastPoint.Value, 3.2, 3.2);
            }
        }

        // Render Left Vertical Voltage Scale & Channel Color Tabs
        RenderLeftVoltageScale(context, marginL, headerH, h, centerY);

        // Draw Interactive Measurement Cursor Lines
        if (ShowCursors)
        {
            RenderMeasurementCursorLines(context, w, h, headerH);
        }

        // Top HUD Header Banner (Provides clean dark background for badges & buttons)
        var headerRect = new Rect(0, 0, w, headerH);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(235, 10, 16, 24)), headerRect);
        context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(80, 28, 44, 62)), 1), new Point(0, headerH), new Point(w, headerH));

        // Channel readout badges inside Top HUD Header
        double textX = 10.0;
        foreach (var trace in Traces)
        {
            if (trace.Samples.Count < 2) continue;

            var samplesList = trace.Samples.ToArray();
            double peakVal = samplesList.Max(s => Math.Abs(s));
            string curFormatted = FormatTraceValue(trace.CurrentValue, trace.Unit);
            string peakFormatted = FormatTraceValue(peakVal, trace.Unit);
            string readout = $"{trace.Name}: {curFormatted} (Peak: {peakFormatted})";
            var labelText = new FormattedText(
                readout,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
                10.5,
                new SolidColorBrush(trace.Color)
            );

            var badgeRect = new Rect(textX - 3, 3, labelText.Width + 6, 20);
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(200, 16, 24, 34)), badgeRect, 4);
            context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromArgb(90, trace.Color.R, trace.Color.G, trace.Color.B)), 1), badgeRect, 4);

            context.DrawText(labelText, new Point(textX, 6));
            textX += labelText.Width + 14;
        }

        // Render live waveform stats ribbon across bottom
        if (!ShowCursors)
        {
            RenderStatsRibbon(context, w, h);
        }

        // Draw Interactive Measurement Cursor Handles (hanging cleanly below header banner)
        if (ShowCursors)
        {
            RenderMeasurementCursorHandles(context, w, h, headerH);
        }
    }

    private void RenderStatsRibbon(DrawingContext context, double w, double h)
    {
        var trace = Traces.FirstOrDefault(t => t.Samples.Count >= 2);
        if (trace == null) return;

        var arr = trace.Samples.ToArray();
        double max = arr.Max();
        double min = arr.Min();
        double range = max - min;
        double sumSq = arr.Sum(s => s * s);
        double rms = Math.Sqrt(sumSq / arr.Length);

        // Estimate frequency from mid-crossings
        double mid = (max + min) / 2.0;
        double freq = 0.0;
        if (range > 0.01)
        {
            var crossings = new List<int>();
            for (int i = 0; i < arr.Length - 1; i++)
            {
                if (arr[i] <= mid && arr[i + 1] > mid)
                {
                    crossings.Add(i);
                }
            }

            if (crossings.Count >= 2)
            {
                double totalSamplesSpan = crossings.Last() - crossings.First();
                double avgPeriodSamples = totalSamplesSpan / (crossings.Count - 1);
                double totalTimeMs = TimebaseMs * 10.0;
                double periodMs = avgPeriodSamples * (totalTimeMs / arr.Length);
                if (periodMs > 1e-4)
                {
                    freq = 1000.0 / periodMs;
                }
            }
        }

        string maxStr = FormatTraceValue(max, trace.Unit);
        string minStr = FormatTraceValue(min, trace.Unit);
        string rangeStr = FormatTraceValue(range, trace.Unit);
        string rmsStr = FormatTraceValue(rms, trace.Unit);
        string freqStr = freq > 1000.0 ? $"{freq / 1000.0:F2} kHz" : $"{freq:F2} Hz";

        string statsLine = $"Max {maxStr}   Min {minStr}   Range {rangeStr}   Freq {freqStr}   RMS {rmsStr}";

        var ft = new FormattedText(
            statsLine,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            9.5,
            new SolidColorBrush(Color.FromRgb(200, 215, 230))
        );

        double dotR = 3.5;
        double dotMargin = 7.0;
        double totalW = dotR * 2 + dotMargin + ft.Width + 16;
        double boxH = 18.0;
        double boxY = h - boxH - 4;

        var box = new Rect(10, boxY, totalW, boxH);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(210, 10, 16, 24)), box, 4);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromArgb(80, trace.Color.R, trace.Color.G, trace.Color.B)), 1), box, 4);

        // Colored trace dot
        var dotCenter = new Point(10 + 8 + dotR, boxY + boxH / 2.0);
        context.DrawEllipse(new SolidColorBrush(trace.Color), null, dotCenter, dotR, dotR);

        // Stats text
        context.DrawText(ft, new Point(10 + 8 + dotR * 2 + dotMargin, boxY + 2.5));
    }

    private void RenderMeasurementCursorLines(DrawingContext context, double w, double h, double headerH)
    {
        double x1 = Cursor1X * w;
        double x2 = Cursor2X * w;

        var c1Color = Color.FromRgb(255, 200, 0);   // Amber Gold
        var c2Color = Color.FromRgb(0, 240, 255);   // Electric Cyan

        var c1Pen = new Pen(new SolidColorBrush(c1Color), 1.5) { DashStyle = DashStyle.Dash };
        var c2Pen = new Pen(new SolidColorBrush(c2Color), 1.5) { DashStyle = DashStyle.Dash };

        context.DrawLine(c1Pen, new Point(x1, headerH), new Point(x1, h));
        context.DrawLine(c2Pen, new Point(x2, headerH), new Point(x2, h));

        // Calculate delta measurements
        double totalTimeMs = TimebaseMs * 10.0;
        double t1 = Cursor1X * totalTimeMs;
        double t2 = Cursor2X * totalTimeMs;
        double dt = Math.Abs(t2 - t1);
        double f = dt > 1e-4 ? (1000.0 / dt) : 0.0;

        // Calculate sample values at cursors for CH1
        double v1 = 0.0, v2 = 0.0;
        if (Traces.Count > 0 && Traces[0].Samples.Count > 1)
        {
            var arr = Traces[0].Samples.ToArray();
            int idx1 = Math.Clamp((int)(Cursor1X * (arr.Length - 1)), 0, arr.Length - 1);
            int idx2 = Math.Clamp((int)(Cursor2X * (arr.Length - 1)), 0, arr.Length - 1);
            v1 = arr[idx1];
            v2 = arr[idx2];
        }
        double dv = Math.Abs(v2 - v1);

        string fStr = f > 1000.0 ? $"{f / 1000.0:F2} kHz" : $"{f:F1} Hz";
        string cursorInfo = $"X1: {t1:F2}ms | X2: {t2:F2}ms | ΔX: {dt:F2}ms ({fStr}) | ΔV1: {dv:F3}V";

        // Horizontal dumbbell delta bar (e.g. o───────── 100 ms Δ ─────────o)
        double dumbbellY = h - 42.0;
        var barPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), 1.5);
        double leftX = Math.Min(x1, x2);
        double rightX = Math.Max(x1, x2);
        context.DrawLine(barPen, new Point(leftX, dumbbellY), new Point(rightX, dumbbellY));

        // Circular end handles
        context.DrawEllipse(new SolidColorBrush(Colors.White), null, new Point(leftX, dumbbellY), 4.0, 4.0);
        context.DrawEllipse(new SolidColorBrush(Colors.White), null, new Point(rightX, dumbbellY), 4.0, 4.0);

        // Center pill badge
        string dtLabel = dt >= 1.0 ? $"{dt:F1} ms Δ" : $"{dt * 1000.0:F0} µs Δ";
        var ftDt = new FormattedText(
            dtLabel,
            System.Globalization.CultureInfo.InvariantCulture,
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
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), 1), pillRect, 8);
        context.DrawText(ftDt, new Point(pillX + 7, pillY + 1.5));

        var ft = new FormattedText(
            cursorInfo,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            10.0,
            new SolidColorBrush(Color.FromRgb(255, 230, 120))
        );

        var box = new Rect(w / 2.0 - ft.Width / 2.0 - 8, h - 22, ft.Width + 16, 18);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(235, 12, 18, 26)), box, 4);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromArgb(120, 255, 200, 0)), 1), box, 4);
        context.DrawText(ft, new Point(w / 2.0 - ft.Width / 2.0, h - 20));
    }

    private void RenderLeftVoltageScale(DrawingContext context, double marginL, double headerH, double h, double centerY)
    {
        var trace = Traces.FirstOrDefault(t => t.Samples.Count >= 2);
        double peakVal = trace != null ? trace.Samples.Max(s => Math.Abs(s)) : 4.0;
        double maxScale = Math.Max(1.0, Math.Ceiling(peakVal));
        double scaleY = (h * 0.38) / Math.Max(1e-12, peakVal);

        // Subtle dark shading for the left margin
        var marginBg = new SolidColorBrush(Color.FromArgb(140, 8, 12, 18));
        context.FillRectangle(marginBg, new Rect(0, headerH, marginL, h - headerH));
        context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(50, 40, 60, 85)), 1), new Point(marginL, headerH), new Point(marginL, h));

        // Render Y-Axis Tick marks and labels (e.g. 0V, 1V, 2V, 3V, 4V)
        int steps = 4;
        for (int i = 0; i <= steps; i++)
        {
            double vLevel = (maxScale / steps) * i;
            double yPos = centerY - vLevel * scaleY;
            if (yPos < headerH + 6 || yPos > h - 6) continue;

            // Tick mark
            var tickPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 160, 190, 220)), 1);
            context.DrawLine(tickPen, new Point(marginL - 4, yPos), new Point(marginL, yPos));

            // Format label e.g. "4 V", "3 V", "2 V", "1 V", "0 V"
            string vStr = $"{vLevel:G3} V";
            var ft = new FormattedText(
                vStr,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
                8.5,
                new SolidColorBrush(Color.FromRgb(160, 180, 205))
            );
            context.DrawText(ft, new Point(marginL - 6 - ft.Width, yPos - ft.Height / 2.0));
        }

        // Render Left Channel Color Tabs on the leftmost bezel edge
        double tabX = 0;
        double tabW = 4.0;
        foreach (var t in Traces)
        {
            if (t.Samples.Count < 2) continue;
            double tY = centerY - t.CurrentValue * scaleY;
            tY = Math.Clamp(tY, headerH + 10, h - 20);

            double tabH = 18.0;
            var tabRect = new Rect(tabX, tY - tabH / 2.0, tabW, tabH);
            context.FillRectangle(new SolidColorBrush(t.Color), tabRect, 2);
        }
    }

    private void RenderMeasurementCursorHandles(DrawingContext context, double w, double h, double headerH)
    {
        double x1 = Cursor1X * w;
        double x2 = Cursor2X * w;

        var c1Color = Color.FromRgb(255, 200, 0);   // Amber Gold
        var c2Color = Color.FromRgb(0, 240, 255);   // Electric Cyan

        // Cursor Handles (Hanging cleanly below the top HUD banner line, completely non-colliding)
        DrawHandle(context, x1, headerH, c1Color, "X1");
        DrawHandle(context, x2, headerH, c2Color, "X2");
    }

    private void DrawHandle(DrawingContext context, double x, double y, Color color, string label)
    {
        // Downward pointer arrow attached to header line
        var handleGeom = new StreamGeometry();
        using (var ctx = handleGeom.Open())
        {
            ctx.BeginFigure(new Point(x - 5, y), isFilled: true);
            ctx.LineTo(new Point(x + 5, y));
            ctx.LineTo(new Point(x, y + 6));
            ctx.EndFigure(isClosed: true);
        }
        context.DrawGeometry(new SolidColorBrush(color), null, handleGeom);

        // Compact pill badge containing label ("X1" / "X2")
        var ft = new FormattedText(
            label,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            8.5,
            new SolidColorBrush(Color.FromRgb(10, 16, 24))
        );

        double badgeW = ft.Width + 6;
        double badgeH = 12;
        var badgeRect = new Rect(x - badgeW / 2.0, y + 6, badgeW, badgeH);
        context.FillRectangle(new SolidColorBrush(color), badgeRect, 2);
        context.DrawText(ft, new Point(x - ft.Width / 2.0, y + 6.5));
    }

    private void RenderFftSpectrum(DrawingContext context, double w, double h)
    {
        double headerH = 26.0;

        // Background Spectrum Grid: dBV levels from -80 dBV to +10 dBV
        double topMargin = 32.0;
        double bottomMargin = 22.0;
        double graphH = h - topMargin - bottomMargin;

        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(35, 0, 200, 255)), 1);
        double[] dbLevels = { 10, 0, -20, -40, -60, -80 };
        double minDb = -80.0;
        double maxDb = 10.0;

        foreach (var db in dbLevels)
        {
            double norm = (maxDb - db) / (maxDb - minDb);
            double y = topMargin + norm * graphH;
            context.DrawLine(gridPen, new Point(35, y), new Point(w - 10, y));

            var dbText = new FormattedText(
                $"{db:+0;-0;0} dB",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
                9,
                new SolidColorBrush(Color.FromRgb(100, 140, 170))
            );
            context.DrawText(dbText, new Point(2, y - 6));
        }

        // Frequency Grid Lines
        for (int i = 1; i < 6; i++)
        {
            double x = 35 + (w - 45) * (i / 6.0);
            context.DrawLine(gridPen, new Point(x, topMargin), new Point(x, topMargin + graphH));
        }

        // Compute FFT for each trace
        double sampleRate = 1000.0 / (TimebaseMs / 25.0); // Approximate sampling rate
        double topPeakFreq = 0.0;
        double topPeakDb = -120.0;
        Color topPeakColor = Color.FromRgb(0, 240, 255);

        foreach (var trace in Traces)
        {
            if (trace.Samples.Count < 16) continue;

            var samples = trace.Samples.ToArray();
            var (freqs, magsDb, peakFreq, peakDb) = ComputeFft(samples, sampleRate);

            if (freqs.Length < 2) continue;

            var pathGeom = new StreamGeometry();
            using (var ctx = pathGeom.Open())
            {
                double startX = 35;
                double startNorm = Math.Clamp((maxDb - magsDb[0]) / (maxDb - minDb), 0.0, 1.0);
                ctx.BeginFigure(new Point(startX, topMargin + startNorm * graphH), isFilled: false);

                double dx = (w - 45) / (freqs.Length - 1);
                for (int i = 1; i < freqs.Length; i++)
                {
                    double x = 35 + i * dx;
                    double norm = Math.Clamp((maxDb - magsDb[i]) / (maxDb - minDb), 0.0, 1.0);
                    ctx.LineTo(new Point(x, topMargin + norm * graphH));
                }
            }

            var glowPen = new Pen(new SolidColorBrush(Color.FromArgb(60, trace.Color.R, trace.Color.G, trace.Color.B)), 4.0);
            var corePen = new Pen(new SolidColorBrush(trace.Color), 1.8);
            context.DrawGeometry(null, glowPen, pathGeom);
            context.DrawGeometry(null, corePen, pathGeom);

            if (peakFreq > 0 && peakDb > topPeakDb)
            {
                topPeakDb = peakDb;
                topPeakFreq = peakFreq;
                topPeakColor = trace.Color;
            }
        }

        // Top HUD Header Banner (Dark background for peak badge & buttons)
        var headerRect = new Rect(0, 0, w, headerH);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(235, 10, 16, 24)), headerRect);
        context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(80, 28, 44, 62)), 1), new Point(0, headerH), new Point(w, headerH));

        // Peak Frequency Badge inside Header Banner
        if (topPeakFreq > 0 && topPeakDb > -70)
        {
            string peakStr = topPeakFreq >= 1000.0 ? $"{topPeakFreq / 1000.0:F2} kHz" : $"{topPeakFreq:F0} Hz";
            var peakText = new FormattedText(
                $"▲ Peak: {peakStr} ({topPeakDb:F1} dBV)",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
                10,
                new SolidColorBrush(topPeakColor)
            );

            double badgeW = peakText.Width + 14;
            double badgeX = _cursorBtnRect.X - badgeW - 8;
            var badge = new Rect(badgeX, 3, badgeW, 20);
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(200, 10, 16, 24)), badge, 4);
            context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromArgb(120, topPeakColor.R, topPeakColor.G, topPeakColor.B)), 1), badge, 4);
            context.DrawText(peakText, new Point(badgeX + 7, 6));
        }

        // Frequency axis labels
        var fStart = new FormattedText("0 Hz", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold), 9, new SolidColorBrush(Color.FromRgb(100, 140, 170)));
        context.DrawText(fStart, new Point(35, h - 16));

        double fMax = sampleRate / 2.0;
        string maxStr = fMax >= 1000 ? $"{fMax / 1000.0:F1} kHz" : $"{fMax:F0} Hz";
        var fEnd = new FormattedText(maxStr, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold), 9, new SolidColorBrush(Color.FromRgb(100, 140, 170)));
        context.DrawText(fEnd, new Point(w - fEnd.Width - 10, h - 16));
    }

    private void RenderInstrumentButtons(DrawingContext context, double w, double h)
    {
        // Time / FFT Mode button
        string modeLabel = DisplayMode == OscilloscopeDisplayMode.TimeDomain ? "MODE: TIME" : "MODE: FFT";
        var ftMode = new FormattedText(
            modeLabel,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            10,
            new SolidColorBrush(DisplayMode == OscilloscopeDisplayMode.FftSpectrum ? Color.FromRgb(0, 240, 255) : Color.FromRgb(0, 230, 140))
        );

        context.FillRectangle(new SolidColorBrush(Color.FromArgb(220, 18, 25, 36)), _modeBtnRect, 4);
        context.DrawRectangle(new Pen(new SolidColorBrush(DisplayMode == OscilloscopeDisplayMode.FftSpectrum ? Color.FromRgb(0, 240, 255) : Color.FromRgb(40, 60, 80)), 1), _modeBtnRect, 4);
        context.DrawText(ftMode, new Point(_modeBtnRect.X + 7, _modeBtnRect.Y + 3.5));

        // Cursors toggle button
        string cursorLabel = ShowCursors ? "CURSORS: ON" : "CURSORS: OFF";
        var ftCursor = new FormattedText(
            cursorLabel,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            10,
            new SolidColorBrush(ShowCursors ? Color.FromRgb(255, 200, 0) : Color.FromRgb(120, 140, 160))
        );

        context.FillRectangle(new SolidColorBrush(Color.FromArgb(220, 18, 25, 36)), _cursorBtnRect, 4);
        context.DrawRectangle(new Pen(new SolidColorBrush(ShowCursors ? Color.FromRgb(255, 200, 0) : Color.FromRgb(40, 60, 80)), 1), _cursorBtnRect, 4);
        context.DrawText(ftCursor, new Point(_cursorBtnRect.X + 7, _cursorBtnRect.Y + 3.5));
    }

    /// <summary>
    /// Pure C# Cooley-Tukey Radix-2 FFT with Hann Windowing for 60 FPS real-time spectral analysis.
    /// </summary>
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
            // Hann window
            double hann = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (n - 1)));
            real[i] = samples[srcOffset + i] * hann;
            imag[i] = 0.0;
        }

        // Bit reversal
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

        // Cooley-Tukey Butterfly
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

            // Ignore DC bin (k=0) when searching for harmonic peak
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
            // Input was in mA: convert to base A and format
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
