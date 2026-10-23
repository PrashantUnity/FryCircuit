using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using FryCircuit.Core.Control;
using FryCircuit.UI.Theming;

namespace FryCircuit.UI.Controls;

/// <summary>
/// High-performance laboratory-grade interactive graphical canvas for Control Systems Studio.
/// Renders Bode frequency response (dual dB/phase), Nyquist stability locus with Cauchy encirclements,
/// Åström Chapter 5 Gang of Four sensitivity matrix, closed-loop Step Response, Root Locus,
/// and analytical problem solving visualization.
/// </summary>
public class ControlStudioCanvas : Control
{
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));

    public static readonly StyledProperty<TransferFunction?> PlantProperty =
        AvaloniaProperty.Register<ControlStudioCanvas, TransferFunction?>(nameof(Plant));

    public static readonly StyledProperty<TransferFunction?> ControllerProperty =
        AvaloniaProperty.Register<ControlStudioCanvas, TransferFunction?>(nameof(Controller));

    public static readonly StyledProperty<string> ActiveTabProperty =
        AvaloniaProperty.Register<ControlStudioCanvas, string>(nameof(ActiveTab), "Bode");

    public static readonly StyledProperty<double> GainKProperty =
        AvaloniaProperty.Register<ControlStudioCanvas, double>(nameof(GainK), 1.0, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> Problem4OvershootProperty =
        AvaloniaProperty.Register<ControlStudioCanvas, double>(nameof(Problem4Overshoot), 9.5);

    public static readonly StyledProperty<double> Problem4SettlingTimeProperty =
        AvaloniaProperty.Register<ControlStudioCanvas, double>(nameof(Problem4SettlingTime), 0.74);

    public static readonly StyledProperty<double> Problem5PoleAProperty =
        AvaloniaProperty.Register<ControlStudioCanvas, double>(nameof(Problem5PoleA), 1.0);

    public static readonly StyledProperty<double> Problem5GainKProperty =
        AvaloniaProperty.Register<ControlStudioCanvas, double>(nameof(Problem5GainK), 5.0);

    public TransferFunction? Plant
    {
        get => GetValue(PlantProperty);
        set => SetValue(PlantProperty, value);
    }

    public TransferFunction? Controller
    {
        get => GetValue(ControllerProperty);
        set => SetValue(ControllerProperty, value);
    }

    public string ActiveTab
    {
        get => GetValue(ActiveTabProperty);
        set => SetValue(ActiveTabProperty, value);
    }

    public double GainK
    {
        get => GetValue(GainKProperty);
        set => SetValue(GainKProperty, value);
    }

    public double Problem4Overshoot
    {
        get => GetValue(Problem4OvershootProperty);
        set => SetValue(Problem4OvershootProperty, value);
    }

    public double Problem4SettlingTime
    {
        get => GetValue(Problem4SettlingTimeProperty);
        set => SetValue(Problem4SettlingTimeProperty, value);
    }

    public double Problem5PoleA
    {
        get => GetValue(Problem5PoleAProperty);
        set => SetValue(Problem5PoleAProperty, value);
    }

    public double Problem5GainK
    {
        get => GetValue(Problem5GainKProperty);
        set => SetValue(Problem5GainKProperty, value);
    }

    private Point? _hoverPos;
    private bool _isDraggingGain;

    static ControlStudioCanvas()
    {
        AffectsRender<ControlStudioCanvas>(
            PlantProperty,
            ControllerProperty,
            ActiveTabProperty,
            GainKProperty,
            Problem4OvershootProperty,
            Problem4SettlingTimeProperty,
            Problem5PoleAProperty,
            Problem5GainKProperty);
    }

    public ControlStudioCanvas()
    {
        ClipToBounds = true;
        CircuitThemeService.Instance.TokensChanged += _ => InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _hoverPos = e.GetPosition(this);

        if (_isDraggingGain && ActiveTab == "RootLocus")
        {
            double w = Bounds.Width;
            double rel = Math.Clamp(_hoverPos.Value.X / Math.Max(w, 1.0), 0.01, 1.0);
            GainK = Math.Round(rel * 50.0, 2);
        }

        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hoverPos = null;
        _isDraggingGain = false;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (ActiveTab == "RootLocus")
        {
            _isDraggingGain = true;
            _hoverPos = e.GetPosition(this);
            double w = Bounds.Width;
            double rel = Math.Clamp(_hoverPos.Value.X / Math.Max(w, 1.0), 0.01, 1.0);
            GainK = Math.Round(rel * 50.0, 2);
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isDraggingGain = false;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w < 40 || h < 40) return;

        var tokens = CircuitThemeService.Instance.Tokens;

        // Instrument background
        var bg = tokens.IsDark
            ? (IBrush)new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(11, 16, 22), 0.0),
                    new GradientStop(Color.FromRgb(7, 10, 15), 1.0)
                }
            }
            : (IBrush)new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(250, 252, 255), 0.0),
                    new GradientStop(Color.FromRgb(242, 246, 250), 1.0)
                }
            };

        context.FillRectangle(bg, new Rect(0, 0, w, h));
        context.DrawRectangle(null, new Pen(tokens.BorderSubtleBrush, 1), new Rect(0.5, 0.5, w - 1, h - 1), 6, 6);

        var p = Plant ?? TransferFunction.One;
        var c = Controller ?? TransferFunction.One;
        var loop = c * p;

        switch (ActiveTab)
        {
            case "Nyquist":
                RenderNyquist(context, loop, w, h, tokens);
                break;
            case "GangOfFour":
                RenderGangOfFour(context, p, c, w, h, tokens);
                break;
            case "StepResponse":
                var cl = loop.Feedback(TransferFunction.One);
                RenderStepResponse(context, cl, w, h, tokens);
                break;
            case "RootLocus":
                RenderRootLocus(context, loop, GainK, w, h, tokens);
                break;
            case "SpecsSynthesis":
            case "ProblemSolver":
                RenderProblemSolver(context, w, h, tokens);
                break;
            case "Bode":
            default:
                RenderBode(context, loop, w, h, tokens);
                break;
        }
    }

    #region Bode Plot Renderer
    private void RenderBode(DrawingContext context, TransferFunction loop, double w, double h, CircuitThemeTokens tokens)
    {
        var bode = BodeAnalyzer.Analyze(loop, 1e-2, 1e3, 300);

        double padL = 60;
        double padR = 25;
        double padT = 28;
        double padB = 30;
        double midGap = 20;

        double plotW = Math.Max(w - padL - padR, 50);
        double totalPlotH = Math.Max(h - padT - padB - midGap, 60);
        double magH = totalPlotH * 0.52;
        double phaseH = totalPlotH * 0.48;

        Rect magRect = new Rect(padL, padT, plotW, magH);
        Rect phaseRect = new Rect(padL, padT + magH + midGap, plotW, phaseH);

        // Grid lines & boundaries
        var gridPen = new Pen(new SolidColorBrush(tokens.IsDark ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(30, 0, 0, 0)), 1);
        var zeroPen = new Pen(new SolidColorBrush(tokens.IsDark ? Color.FromArgb(120, 255, 255, 255) : Color.FromArgb(90, 0, 0, 0)), 1, DashStyle.Dash);
        var marginPen = new Pen(tokens.WarningBrush, 1.2, DashStyle.Dash);

        context.DrawRectangle(null, new Pen(tokens.BorderSubtleBrush, 1), magRect);
        context.DrawRectangle(null, new Pen(tokens.BorderSubtleBrush, 1), phaseRect);

        // Titles
        DrawText(context, "BODE MAGNITUDE |L(jω)| (dB)", magRect.Left + 8, magRect.Top + 6, tokens.AccentBrush, 10, true);
        DrawText(context, "BODE PHASE ∠L(jω) (deg)", phaseRect.Left + 8, phaseRect.Top + 6, tokens.SecondaryBrush, 10, true);

        // Log Frequency Decades: 1e-2 to 1e3 (5 decades)
        double minLog = Math.Log10(1e-2);
        double maxLog = Math.Log10(1e3);

        for (int dec = -2; dec <= 3; dec++)
        {
            double freq = Math.Pow(10, dec);
            double x = magRect.Left + (dec - minLog) / (maxLog - minLog) * plotW;

            context.DrawLine(gridPen, new Point(x, magRect.Top), new Point(x, magRect.Bottom));
            context.DrawLine(gridPen, new Point(x, phaseRect.Top), new Point(x, phaseRect.Bottom));

            // Frequency label on bottom
            string fLabel = dec switch
            {
                -2 => "0.01",
                -1 => "0.1",
                0 => "1",
                1 => "10",
                2 => "100",
                3 => "1k rad/s",
                _ => $"{freq}"
            };
            DrawText(context, fLabel, x - 10, phaseRect.Bottom + 6, tokens.TextMutedBrush, 8.5);

            // Sub-decade lines (2, 3, 4, 5, 6, 7, 8, 9)
            if (dec < 3)
            {
                for (int m = 2; m <= 9; m++)
                {
                    double subF = freq * m;
                    double subX = magRect.Left + (Math.Log10(subF) - minLog) / (maxLog - minLog) * plotW;
                    context.DrawLine(new Pen(new SolidColorBrush(tokens.IsDark ? Color.FromArgb(18, 255, 255, 255) : Color.FromArgb(14, 0, 0, 0)), 0.8),
                        new Point(subX, magRect.Top), new Point(subX, magRect.Bottom));
                    context.DrawLine(new Pen(new SolidColorBrush(tokens.IsDark ? Color.FromArgb(18, 255, 255, 255) : Color.FromArgb(14, 0, 0, 0)), 0.8),
                        new Point(subX, phaseRect.Top), new Point(subX, phaseRect.Bottom));
                }
            }
        }

        // Magnitude Axis Range: -60 dB to +40 dB
        double minDb = -60.0;
        double maxDb = 40.0;
        for (double db = minDb; db <= maxDb; db += 20.0)
        {
            double y = magRect.Bottom - (db - minDb) / (maxDb - minDb) * magH;
            context.DrawLine(db == 0.0 ? zeroPen : gridPen, new Point(magRect.Left, y), new Point(magRect.Right, y));
            DrawText(context, $"{db:0} dB", padL - 45, y - 6, db == 0.0 ? tokens.TextBrush : tokens.TextMutedBrush, 8.5);
        }

        // Phase Axis Range: -270 deg to +90 deg
        double minDeg = -270.0;
        double maxDeg = 90.0;
        for (double deg = minDeg; deg <= maxDeg; deg += 45.0)
        {
            double y = phaseRect.Bottom - (deg - minDeg) / (maxDeg - minDeg) * phaseH;
            context.DrawLine(deg == -180.0 ? zeroPen : gridPen, new Point(phaseRect.Left, y), new Point(phaseRect.Right, y));
            DrawText(context, $"{deg:0}°", padL - 45, y - 6, deg == -180.0 ? tokens.TextBrush : tokens.TextMutedBrush, 8.5);
        }

        // Plot curves
        var magPoints = new List<Point>();
        var phasePoints = new List<Point>();

        foreach (var pt in bode.Points)
        {
            double x = magRect.Left + (Math.Log10(pt.Omega) - minLog) / (maxLog - minLog) * plotW;
            double yMag = Math.Clamp(magRect.Bottom - (pt.MagnitudeDb - minDb) / (maxDb - minDb) * magH, magRect.Top, magRect.Bottom);
            double yPhase = Math.Clamp(phaseRect.Bottom - (pt.PhaseDeg - minDeg) / (maxDeg - minDeg) * phaseH, phaseRect.Top, phaseRect.Bottom);

            magPoints.Add(new Point(x, yMag));
            phasePoints.Add(new Point(x, yPhase));
        }

        DrawPolyline(context, magPoints, tokens.AccentBrush, 2.0);
        DrawPolyline(context, phasePoints, tokens.SecondaryBrush, 2.0);

        // Gain Crossover Frequency (w_gc) and Phase Margin (PM)
        if (bode.GainCrossoverFrequency.HasValue)
        {
            double wgc = bode.GainCrossoverFrequency.Value;
            double xGc = magRect.Left + (Math.Log10(wgc) - minLog) / (maxLog - minLog) * plotW;

            if (xGc >= magRect.Left && xGc <= magRect.Right)
            {
                context.DrawLine(marginPen, new Point(xGc, magRect.Top), new Point(xGc, phaseRect.Bottom));
                double pmVal = bode.PhaseMarginDeg ?? 0.0;
                double yPhaseGc = Math.Clamp(phaseRect.Bottom - (pmVal - 180.0 - minDeg) / (maxDeg - minDeg) * phaseH, phaseRect.Top, phaseRect.Bottom);
                double y180 = phaseRect.Bottom - (-180.0 - minDeg) / (maxDeg - minDeg) * phaseH;

                // Highlight Phase Margin vertical bar
                context.DrawLine(new Pen(tokens.WarningBrush, 3.0), new Point(xGc, y180), new Point(xGc, yPhaseGc));
                context.DrawEllipse(tokens.WarningBrush, null, new Point(xGc, yPhaseGc), 3.5, 3.5);

                DrawText(context, $"ωgc={wgc:F2}", xGc + 4, magRect.Top + 20, tokens.WarningBrush, 8.5, true);
                DrawText(context, $"PM={pmVal:F1}°", xGc + 4, yPhaseGc - 6, tokens.WarningBrush, 8.5, true);
            }
        }

        // Phase Crossover Frequency (w_pc) and Gain Margin (GM)
        if (bode.PhaseCrossoverFrequency.HasValue)
        {
            double wpc = bode.PhaseCrossoverFrequency.Value;
            double xPc = magRect.Left + (Math.Log10(wpc) - minLog) / (maxLog - minLog) * plotW;

            if (xPc >= magRect.Left && xPc <= magRect.Right)
            {
                context.DrawLine(marginPen, new Point(xPc, magRect.Top), new Point(xPc, phaseRect.Bottom));
                double y0 = magRect.Bottom - (0.0 - minDb) / (maxDb - minDb) * magH;
                double gmVal = bode.GainMarginDb ?? 0.0;
                double yMagPc = Math.Clamp(magRect.Bottom - (-gmVal - minDb) / (maxDb - minDb) * magH, magRect.Top, magRect.Bottom);

                // Highlight Gain Margin vertical bar
                context.DrawLine(new Pen(tokens.WarningBrush, 3.0), new Point(xPc, y0), new Point(xPc, yMagPc));
                context.DrawEllipse(tokens.WarningBrush, null, new Point(xPc, yMagPc), 3.5, 3.5);

                DrawText(context, $"ωpc={wpc:F2}", xPc + 4, phaseRect.Top + 20, tokens.WarningBrush, 8.5, true);
                DrawText(context, $"GM={gmVal:F1}dB", xPc + 4, yMagPc - 6, tokens.WarningBrush, 8.5, true);
            }
        }

        // Interactive Mouse Crosshair Cursor
        if (_hoverPos.HasValue && _hoverPos.Value.X >= magRect.Left && _hoverPos.Value.X <= magRect.Right)
        {
            double hx = _hoverPos.Value.X;
            double frac = (hx - magRect.Left) / plotW;
            double hoverW = Math.Pow(10, minLog + frac * (maxLog - minLog));
            var resp = loop.Evaluate(new Complex(0, hoverW));
            double hDb = 20.0 * Math.Log10(Math.Max(resp.Magnitude, 1e-12));
            double hDeg = resp.Phase * 180.0 / Math.PI;

            var crossPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 160, 200, 255)), 1, DashStyle.Dash);
            context.DrawLine(crossPen, new Point(hx, magRect.Top), new Point(hx, phaseRect.Bottom));

            // Tooltip badge
            string tip = $"ω: {hoverW:F2} rad/s | |L|: {hDb:F1} dB | ∠L: {hDeg:F1}°";
            double badgeW = 220;
            double badgeH = 20;
            double badgeX = Math.Clamp(hx - badgeW / 2, magRect.Left, magRect.Right - badgeW);
            double badgeY = magRect.Top + 4;

            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(220, 15, 23, 42)), new Pen(tokens.AccentBrush, 1), new Rect(badgeX, badgeY, badgeW, badgeH), 4, 4);
            DrawText(context, tip, badgeX + 8, badgeY + 4, Brushes.White, 9, true);
        }
    }
    #endregion

    #region Nyquist Plot Renderer
    private void RenderNyquist(DrawingContext context, TransferFunction loop, double w, double h, CircuitThemeTokens tokens)
    {
        var nyquist = NyquistAnalyzer.Analyze(loop, 1e-2, 1e3, 500);

        double pad = 35;
        double plotW = w - pad * 2;
        double plotH = h - pad * 2;
        double cx = pad + plotW / 2;
        double cy = pad + plotH / 2;

        // Auto-scale complex plane
        double maxRe = Math.Max(nyquist.Points.Max(p => Math.Abs(p.Real)), 2.5);
        double maxIm = Math.Max(nyquist.Points.Max(p => Math.Abs(p.Imaginary)), 2.5);
        double span = Math.Min(Math.Max(maxRe, maxIm) * 1.25, 12.0);

        double scale = Math.Min(plotW, plotH) / (2.0 * span);

        var gridPen = new Pen(new SolidColorBrush(tokens.IsDark ? Color.FromArgb(35, 255, 255, 255) : Color.FromArgb(25, 0, 0, 0)), 1);
        var axisPen = new Pen(new SolidColorBrush(tokens.IsDark ? Color.FromArgb(120, 255, 255, 255) : Color.FromArgb(90, 0, 0, 0)), 1);

        // Draw axes
        context.DrawLine(axisPen, new Point(pad, cy), new Point(w - pad, cy));
        context.DrawLine(axisPen, new Point(cx, pad), new Point(cx, h - pad));

        DrawText(context, "Real Re(L)", w - pad - 50, cy - 14, tokens.TextMutedBrush, 9);
        DrawText(context, "Imag Im(L)", cx + 6, pad + 4, tokens.TextMutedBrush, 9);
        DrawText(context, "NYQUIST POLAR LOCUS & CAUCHY STABILITY (Z = N + P)", pad + 8, pad + 6, tokens.AccentBrush, 10, true);

        // Concentric unit circles
        for (int r = 1; r <= (int)span; r++)
        {
            double rPix = r * scale;
            context.DrawEllipse(null, gridPen, new Point(cx, cy), rPix, rPix);
            DrawText(context, $"{r}", cx + rPix + 2, cy + 2, tokens.TextMutedBrush, 7.5);
        }

        // Unit circle (|s| = 1) in dashed line
        context.DrawEllipse(null, new Pen(tokens.TextMutedBrush, 1, DashStyle.Dash), new Point(cx, cy), scale, scale);

        // Critical Point (-1, 0)
        double critX = cx - 1.0 * scale;
        double critY = cy;
        var critPen = new Pen(tokens.DangerBrush, 2);
        context.DrawLine(critPen, new Point(critX - 6, critY - 6), new Point(critX + 6, critY + 6));
        context.DrawLine(critPen, new Point(critX - 6, critY + 6), new Point(critX + 6, critY - 6));
        DrawText(context, "(-1, j0)", critX - 22, critY + 8, tokens.DangerBrush, 9, true);

        // Distance circle from (-1, 0) of radius 1/Ms (Astrom robustness margin)
        if (nyquist.PeakSensitivity > 0 && !double.IsInfinity(nyquist.PeakSensitivity))
        {
            double rDist = (1.0 / nyquist.PeakSensitivity) * scale;
            context.DrawEllipse(null, new Pen(tokens.WarningBrush, 1, DashStyle.Dash), new Point(critX, critY), rDist, rDist);
            DrawText(context, $"1/Ms = {1.0 / nyquist.PeakSensitivity:F2}", critX - rDist - 30, critY - 12, tokens.WarningBrush, 8);
        }

        // Nyquist locus curve (w > 0: solid; w < 0: dashed conjugate)
        var posPoints = new List<Point>();
        var negPoints = new List<Point>();

        foreach (var pt in nyquist.Points)
        {
            double px = cx + pt.Real * scale;
            double py = cy - pt.Imaginary * scale;
            posPoints.Add(new Point(px, py));

            // Conjugate for negative frequencies
            double npy = cy + pt.Imaginary * scale;
            negPoints.Add(new Point(px, npy));
        }

        DrawPolyline(context, posPoints, tokens.AccentBrush, 2.0);
        DrawPolyline(context, negPoints, tokens.SecondaryBrush, 1.2, DashStyle.Dash);

        // Cauchy Encirclement Counter Badge
        string stabText = nyquist.IsClosedLoopStable ? "STABLE (Z = 0)" : "UNSTABLE (Z > 0)";
        var stabBrush = nyquist.IsClosedLoopStable ? SuccessBrush : tokens.DangerBrush;

        double badgeW = 230;
        double badgeH = 75;
        double bx = pad + 10;
        double by = h - pad - badgeH - 5;

        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(220, 15, 23, 42)), new Pen(stabBrush, 1.5), new Rect(bx, by, badgeW, badgeH), 6, 6);

        DrawText(context, $"Nyquist Criterion: Z = N + P", bx + 10, by + 6, Brushes.White, 9.5, true);
        DrawText(context, $"Clockwise Encirclements N: {nyquist.ClockwiseEncirclements}", bx + 10, by + 22, tokens.TextBrush, 8.5);
        DrawText(context, $"Open-Loop RHP Poles P: {nyquist.OpenLoopRhpPoles}", bx + 10, by + 36, tokens.TextBrush, 8.5);
        DrawText(context, $"Closed-Loop RHP Poles Z: {nyquist.ClosedLoopRhpPoles}", bx + 10, by + 50, tokens.TextBrush, 8.5);
        DrawText(context, stabText, bx + 130, by + 50, stabBrush, 9, true);
    }
    #endregion

    #region Gang of Four (Astrom Chapter 5) Renderer
    private void RenderGangOfFour(DrawingContext context, TransferFunction p, TransferFunction c, double w, double h, CircuitThemeTokens tokens)
    {
        var gof = GangOfFourAnalyzer.Analyze(p, c);

        double pad = 25;
        double gridW = (w - pad * 3) / 2.0;
        double gridH = (h - pad * 3) / 2.0;

        Rect q1 = new Rect(pad, pad, gridW, gridH);                     // S(s)
        Rect q2 = new Rect(pad * 2 + gridW, pad, gridW, gridH);         // T(s)
        Rect q3 = new Rect(pad, pad * 2 + gridH, gridW, gridH);         // PS(s)
        Rect q4 = new Rect(pad * 2 + gridW, pad * 2 + gridH, gridW, gridH); // CS(s)

        RenderSingleGangPlot(context, q1, gof.Sensitivity, "Sensitivity S = 1/(1+PC)", $"Peak Ms = {gof.PeakSensitivityMs:F2} | Bandwidth wb = {gof.DisturbanceBandwidthWb:F2} rad/s", tokens.AccentBrush, tokens, showMinus3Db: true);
        RenderSingleGangPlot(context, q2, gof.CompSensitivity, "Complementary Sensitivity T = PC/(1+PC)", $"Peak Mt = {gof.PeakCompSensitivityMt:F2}", SuccessBrush, tokens);
        RenderSingleGangPlot(context, q3, gof.LoadDisturbance, "Load Disturbance Sensitivity PS = P/(1+PC)", "Output response to load disturbance d", tokens.WarningBrush, tokens);
        RenderSingleGangPlot(context, q4, gof.NoiseControl, "Noise Sensitivity CS = C/(1+PC)", "Control effort from sensor noise n", tokens.SecondaryBrush, tokens);

        // Waterbed Effect Display Banner
        string wb = $"Bode Sensitivity Integral: {gof.BodeSensitivityIntegralValue:F3}  (Theoretical Waterbed Target: {gof.TheoreticalWaterbedTarget:F3})";
        DrawText(context, wb, pad + 10, h - 16, tokens.AccentBrush, 9, true);
    }

    private void RenderSingleGangPlot(DrawingContext context, Rect rect, TransferFunction tf, string title, string subtitle, IBrush curveBrush, CircuitThemeTokens tokens, bool showMinus3Db = false)
    {
        context.DrawRectangle(null, new Pen(tokens.BorderSubtleBrush, 1), rect);
        DrawText(context, title, rect.Left + 6, rect.Top + 4, tokens.TextBrush, 9.5, true);
        DrawText(context, subtitle, rect.Left + 6, rect.Top + 18, tokens.TextMutedBrush, 8);

        double minLog = Math.Log10(1e-2);
        double maxLog = Math.Log10(1e2);
        double minDb = -60;
        double maxDb = 30;

        var gridPen = new Pen(new SolidColorBrush(tokens.IsDark ? Color.FromArgb(25, 255, 255, 255) : Color.FromArgb(20, 0, 0, 0)), 0.8);
        var zeroPen = new Pen(new SolidColorBrush(tokens.IsDark ? Color.FromArgb(80, 255, 255, 255) : Color.FromArgb(60, 0, 0, 0)), 1, DashStyle.Dash);

        // Horizontal 0 dB line
        double y0 = rect.Bottom - (0.0 - minDb) / (maxDb - minDb) * (rect.Height - 35);
        context.DrawLine(zeroPen, new Point(rect.Left, y0), new Point(rect.Right, y0));

        if (showMinus3Db)
        {
            double ym3 = rect.Bottom - (-3.0 - minDb) / (maxDb - minDb) * (rect.Height - 35);
            context.DrawLine(new Pen(tokens.WarningBrush, 1, DashStyle.Dash), new Point(rect.Left, ym3), new Point(rect.Right, ym3));
            DrawText(context, "-3 dB", rect.Right - 32, ym3 - 5, tokens.WarningBrush, 7.5);
        }

        var pts = new List<Point>();
        int count = 120;
        for (int i = 0; i <= count; i++)
        {
            double logW = minLog + (double)i / count * (maxLog - minLog);
            double w = Math.Pow(10, logW);
            var val = tf.Evaluate(w);
            double magDb = 20.0 * Math.Log10(Math.Max(val.Magnitude, 1e-12));

            double x = rect.Left + (logW - minLog) / (maxLog - minLog) * rect.Width;
            double y = Math.Clamp(rect.Bottom - (magDb - minDb) / (maxDb - minDb) * (rect.Height - 35), rect.Top + 35, rect.Bottom);
            pts.Add(new Point(x, y));
        }

        DrawPolyline(context, pts, curveBrush, 1.8);
    }
    #endregion

    #region Step Response Renderer
    private void RenderStepResponse(DrawingContext context, TransferFunction cl, double w, double h, CircuitThemeTokens tokens)
    {
        double tMax = Math.Max(cl.Poles.Count > 0 ? 5.0 / Math.Max(Math.Abs(cl.Poles.Min(p => p.Real)), 0.1) : 5.0, 2.0);
        tMax = Math.Min(tMax, 20.0);

        var step = StepResponseAnalyzer.Analyze(cl, tMax, 300);

        double padL = 50;
        double padR = 25;
        double padT = 30;
        double padB = 35;

        double plotW = w - padL - padR;
        double plotH = h - padT - padB;
        Rect plotRect = new Rect(padL, padT, plotW, plotH);

        context.DrawRectangle(null, new Pen(tokens.BorderSubtleBrush, 1), plotRect);
        DrawText(context, "CLOSED-LOOP STEP RESPONSE y(t) • TRANSIENT SPECIFICATIONS", padL + 8, padT + 6, tokens.AccentBrush, 10, true);

        double yMax = Math.Max(step.Points.Max(p => p.Value) * 1.25, 1.4);
        double yMin = 0.0;

        var gridPen = new Pen(new SolidColorBrush(tokens.IsDark ? Color.FromArgb(30, 255, 255, 255) : Color.FromArgb(20, 0, 0, 0)), 0.8);
        var refPen = new Pen(new SolidColorBrush(tokens.IsDark ? Color.FromArgb(120, 255, 255, 255) : Color.FromArgb(90, 0, 0, 0)), 1.2, DashStyle.Dash);

        // Reference input = 1.0
        double yRef = plotRect.Bottom - (1.0 - yMin) / (yMax - yMin) * plotH;
        context.DrawLine(refPen, new Point(plotRect.Left, yRef), new Point(plotRect.Right, yRef));
        DrawText(context, "r(t) = 1.0", plotRect.Right - 55, yRef - 10, tokens.TextBrush, 8.5);

        // 2% Settling Tolerance Band [0.98, 1.02]
        double yBandHi = plotRect.Bottom - (1.02 - yMin) / (yMax - yMin) * plotH;
        double yBandLo = plotRect.Bottom - (0.98 - yMin) / (yMax - yMin) * plotH;
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(20, 56, 189, 248)), new Rect(plotRect.Left, yBandHi, plotW, yBandLo - yBandHi));

        // Time grid
        for (double t = 0; t <= tMax; t += (tMax / 5.0))
        {
            double x = plotRect.Left + (t / tMax) * plotW;
            context.DrawLine(gridPen, new Point(x, plotRect.Top), new Point(x, plotRect.Bottom));
            DrawText(context, $"{t:F1}s", x - 8, plotRect.Bottom + 6, tokens.TextMutedBrush, 8.5);
        }

        // Response curve
        var pts = new List<Point>();
        foreach (var pt in step.Points)
        {
            double x = plotRect.Left + (pt.Time / tMax) * plotW;
            double y = Math.Clamp(plotRect.Bottom - (pt.Value - yMin) / (yMax - yMin) * plotH, plotRect.Top, plotRect.Bottom);
            pts.Add(new Point(x, y));
        }

        DrawPolyline(context, pts, tokens.AccentBrush, 2.2);

        // Annotations: Peak Overshoot & Settling Time
        if (step.PeakTime > 0)
        {
            double xPeak = plotRect.Left + (step.PeakTime / tMax) * plotW;
            double yPeak = plotRect.Bottom - ((1.0 + step.OvershootPercent / 100.0) - yMin) / (yMax - yMin) * plotH;
            context.DrawEllipse(tokens.WarningBrush, null, new Point(xPeak, yPeak), 4, 4);
            DrawText(context, $"Tp={step.PeakTime:F2}s, OS={step.OvershootPercent:F1}%", xPeak + 6, yPeak - 12, tokens.WarningBrush, 8.5, true);
        }

        if (step.SettlingTime > 0 && step.SettlingTime <= tMax)
        {
            double xSet = plotRect.Left + (step.SettlingTime / tMax) * plotW;
            context.DrawLine(new Pen(SuccessBrush, 1.2, DashStyle.Dash), new Point(xSet, plotRect.Top), new Point(xSet, plotRect.Bottom));
            DrawText(context, $"Ts={step.SettlingTime:F2}s", xSet + 4, plotRect.Bottom - 25, SuccessBrush, 8.5, true);
        }

        // Metrics Badge
        string metrics = $"Rise Time Tr: {step.RiseTime:F2}s | Peak Overshoot: {step.OvershootPercent:F1}% | Settling Time Ts: {step.SettlingTime:F2}s | Steady Error ess: {step.SteadyStateError:F3}";
        DrawText(context, metrics, padL + 8, h - 14, tokens.TextBrush, 9, true);
    }
    #endregion

    #region Root Locus Renderer
    private void RenderRootLocus(DrawingContext context, TransferFunction loop, double currentGainK, double w, double h, CircuitThemeTokens tokens)
    {
        var locus = RootLocusAnalyzer.Analyze(loop, 100.0, 200);

        double pad = 35;
        double plotW = w - pad * 2;
        double plotH = h - pad * 2;
        double cx = pad + plotW * 0.7; // Place imaginary axis slightly right of center since stable poles are LHP
        double cy = pad + plotH / 2;

        double span = 8.0;
        double scale = Math.Min(plotW, plotH) / (2.0 * span);

        var gridPen = new Pen(new SolidColorBrush(tokens.IsDark ? Color.FromArgb(30, 255, 255, 255) : Color.FromArgb(20, 0, 0, 0)), 1);
        var axisPen = new Pen(new SolidColorBrush(tokens.IsDark ? Color.FromArgb(120, 255, 255, 255) : Color.FromArgb(90, 0, 0, 0)), 1);

        // Axes
        context.DrawLine(axisPen, new Point(pad, cy), new Point(w - pad, cy));
        context.DrawLine(axisPen, new Point(cx, pad), new Point(cx, h - pad));
        DrawText(context, "Real σ", w - pad - 40, cy - 14, tokens.TextMutedBrush, 9);
        DrawText(context, "Imag jω", cx + 6, pad + 4, tokens.TextMutedBrush, 9);
        DrawText(context, $"ROOT LOCUS IN s-PLANE • GAIN K = {currentGainK:F2} (Click & drag to vary)", pad + 8, pad + 6, tokens.AccentBrush, 10, true);

        // Open-Loop Poles: 'X'
        foreach (var pole in loop.Poles)
        {
            double px = cx + pole.Real * scale;
            double py = cy - pole.Imaginary * scale;
            var polePen = new Pen(tokens.DangerBrush, 2.0);
            context.DrawLine(polePen, new Point(px - 5, py - 5), new Point(px + 5, py + 5));
            context.DrawLine(polePen, new Point(px - 5, py + 5), new Point(px + 5, py - 5));
        }

        // Open-Loop Zeros: 'O'
        foreach (var zero in loop.Zeros)
        {
            double zx = cx + zero.Real * scale;
            double zy = cy - zero.Imaginary * scale;
            context.DrawEllipse(null, new Pen(tokens.AccentBrush, 2.0), new Point(zx, zy), 5, 5);
        }

        // Locus Branches
        if (locus.Trajectories.Count > 0)
        {
            int numRoots = locus.Trajectories[0].Roots.Count;
            for (int r = 0; r < numRoots; r++)
            {
                var branchPts = new List<Point>();
                foreach (var traj in locus.Trajectories)
                {
                    if (r < traj.Roots.Count)
                    {
                        var pt = traj.Roots[r];
                        branchPts.Add(new Point(cx + pt.Real * scale, cy - pt.Imaginary * scale));
                    }
                }
                DrawPolyline(context, branchPts, tokens.SecondaryBrush, 1.8);
            }
        }

        // Current Closed-Loop Poles at Gain K
        var clPoly = loop.Denominator + (loop.Numerator * currentGainK);
        var clPoles = clPoly.Roots();
        foreach (var clPole in clPoles)
        {
            double clx = cx + clPole.Real * scale;
            double cly = cy - clPole.Imaginary * scale;
            context.DrawEllipse(tokens.WarningBrush, new Pen(Brushes.White, 1.5), new Point(clx, cly), 5.5, 5.5);
            DrawText(context, $"{clPole.Real:F2} + j{clPole.Imaginary:F2}", clx + 7, cly - 6, tokens.WarningBrush, 8);
        }
    }
    #endregion

    #region Problem Solver Blackboard Renderer (Academic Textbook Grade)
    private void RenderProblemSolver(DrawingContext context, double w, double h, CircuitThemeTokens tokens)
    {
        double pad = 16.0;
        double halfW = (w - pad * 3.0) / 2.0;
        double panelH = Math.Max(h - pad * 2.0, 520.0);

        Rect leftRect = new Rect(pad, pad, halfW, panelH);
        Rect rightRect = new Rect(pad * 2.0 + halfW, pad, halfW, panelH);

        // Academic chalkboard/slate theme background with subtle borders
        var panelBg = tokens.IsDark
            ? new SolidColorBrush(Color.FromArgb(50, 15, 23, 42))
            : new SolidColorBrush(Color.FromArgb(40, 241, 245, 249));

        context.DrawRectangle(panelBg, new Pen(tokens.BorderSubtleBrush, 1.0), leftRect, 8, 8);
        context.DrawRectangle(panelBg, new Pen(tokens.BorderSubtleBrush, 1.0), rightRect, 8, 8);

        // =========================================================================
        // LEFT PANEL: TRANSIENT SPECIFICATION SYNTHESIS (%OS, Ts -> zeta, wn, K, a)
        // =========================================================================
        var p4 = ControlSpecsSolver.SolveProblem4Specs(Problem4Overshoot, Problem4SettlingTime);
        double lx = leftRect.Left + 14.0;
        double ly = leftRect.Top + 14.0;
        double cardW = halfW - 28.0;

        // Panel Header
        DrawBadge(context, "SPECS SYNTHESIS", lx, ly, tokens.AccentBrush);
        ly += 18.0;
        DrawText(context, "TRANSIENT SPECIFICATION SYNTHESIS", lx, ly, tokens.AccentBrush, 11.0, bold: true);
        ly += 16.0;
        DrawText(context, "Second-order prototype parameter synthesis: (%OS, Ts) ⟶ (ζ, ωn, K, a)", lx, ly, tokens.TextMutedBrush, 8.5);
        ly += 16.0;

        // Given Specs Card
        ly = DrawCard(context, lx, ly, cardW, 26.0, tokens);
        DrawText(context, $"Given Specs:  %OS = {Problem4Overshoot:F1}%   |   Settling Time Ts = {Problem4SettlingTime:F2}s (2% criterion)", lx + 8.0, ly - 20.0, tokens.TextBrush, 9.0, bold: true);
        ly += 6.0;

        // Step 1: Damping Ratio
        double step1H = 54.0;
        ly = DrawCard(context, lx, ly, cardW, step1H, tokens);
        double c1Y = ly - step1H + 6.0;
        DrawText(context, "Step 1 • Damping Ratio Formula (from Logarithmic Decrement):", lx + 8.0, c1Y, tokens.TextMutedBrush, 8.5);
        c1Y += 14.0;
        DrawMathFraction(
            context,
            prefix: "ζ = ",
            num: "-ln(%OS / 100)",
            den: "√(π² + ln²(%OS / 100))",
            suffix: $" = {p4.DampingRatioZeta:F4}",
            x: lx + 12.0,
            y: c1Y,
            textBrush: tokens.TextBrush,
            barBrush: tokens.AccentBrush,
            fontSize: 9.0,
            suffixBrush: SuccessBrush,
            boldSuffix: true,
            secondarySuffix: "  (Underdamped: 0 < ζ < 1)",
            secondaryBrush: tokens.TextDimBrush
        );
        ly += 6.0;

        // Step 2: Natural Frequency
        double step2H = 54.0;
        ly = DrawCard(context, lx, ly, cardW, step2H, tokens);
        double c2Y = ly - step2H + 6.0;
        DrawText(context, "Step 2 • Undamped Natural Frequency (from Settling Time Ts = 4 / (ζ·ωn)):", lx + 8.0, c2Y, tokens.TextMutedBrush, 8.5);
        c2Y += 14.0;
        DrawMathFraction(
            context,
            prefix: "ωn = ",
            num: "4",
            den: "ζ · Ts",
            suffix: $" = {p4.NaturalFrequencyWn:F3} rad/s",
            x: lx + 12.0,
            y: c2Y,
            textBrush: tokens.TextBrush,
            barBrush: tokens.AccentBrush,
            fontSize: 9.0,
            suffixBrush: SuccessBrush,
            boldSuffix: true
        );
        ly += 6.0;

        // Step 3: Characteristic Polynomial Matching
        double step3H = 74.0;
        ly = DrawCard(context, lx, ly, cardW, step3H, tokens);
        double c3Y = ly - step3H + 6.0;
        DrawText(context, "Step 3 • Polynomial Equivalence & Parameter Synthesis:", lx + 8.0, c3Y, tokens.TextMutedBrush, 8.5);
        c3Y += 14.0;
        DrawText(context, "Characteristic Eq:  s² + a·s + K  ≡  s² + 2ζωn·s + ωn²", lx + 12.0, c3Y, tokens.TextBrush, 9.0, bold: true);
        c3Y += 18.0;

        // Boxed Key Solutions
        var boxFill = tokens.IsDark
            ? new SolidColorBrush(Color.FromArgb(35, 245, 158, 11))
            : new SolidColorBrush(Color.FromArgb(25, 245, 158, 11));
        var box1 = DrawMathBox(context, $"K = ωn² = {p4.RequiredGainK:F2}", lx + 12.0, c3Y, tokens.WarningBrush, tokens.WarningBrush, boxFill, 9.5);
        DrawMathBox(context, $"a = 2ζωn = {p4.ParameterA:F2}", box1.Right + 12.0, c3Y, tokens.WarningBrush, tokens.WarningBrush, boxFill, 9.5);
        ly += 6.0;

        // Step 4: Damped Frequency, Peak Time & Closed-Loop Poles
        double step4H = 78.0;
        ly = DrawCard(context, lx, ly, cardW, step4H, tokens);
        double c4Y = ly - step4H + 6.0;
        DrawText(context, "Step 4 • Transient Verification & Dominant Pole Placement:", lx + 8.0, c4Y, tokens.TextMutedBrush, 8.5);
        c4Y += 14.0;

        DrawMathFraction(
            context,
            prefix: $"ωd = ωn√(1 - ζ²) = {p4.DampedFrequencyWd:F3} rad/s    |    Tp = ",
            num: "π",
            den: "ωd",
            suffix: $" = {p4.PeakTimeTp:F3} s",
            x: lx + 12.0,
            y: c4Y,
            textBrush: tokens.TextBrush,
            barBrush: tokens.AccentBrush,
            fontSize: 8.5,
            suffixBrush: tokens.TextBrush,
            boldSuffix: true
        );
        c4Y += 28.0;

        var poleFill = tokens.IsDark
            ? new SolidColorBrush(Color.FromArgb(30, 6, 182, 212))
            : new SolidColorBrush(Color.FromArgb(20, 6, 182, 212));
        var poleBox = DrawMathBox(context, $"Closed-Loop Poles: s₁,₂ = -ζωn ± jωd = {p4.Pole1.Real:F2} ± j{p4.Pole1.Imaginary:F2}", lx + 12.0, c4Y, tokens.AccentBrush, tokens.AccentBrush, poleFill, 9.5);
        DrawText(context, "✓ Hurwitz Stable (LHP)", poleBox.Right + 10.0, c4Y + 4.0, SuccessBrush, 9.0, bold: true);

        // =========================================================================
        // RIGHT PANEL: SPEED PLANT SENSITIVITY & DISTURBANCE REJECTION (Problem 5)
        // =========================================================================
        var p5 = ControlSpecsSolver.SolveProblem5SpeedPlant(Problem5PoleA, Problem5GainK);
        double rx = rightRect.Left + 14.0;
        double ry = rightRect.Top + 14.0;

        // Panel Header
        DrawBadge(context, "BODE SENSITIVITY", rx, ry, tokens.SecondaryBrush);
        ry += 18.0;
        DrawText(context, "SPEED PLANT SENSITIVITY & DISTURBANCE REJECTION", rx, ry, tokens.SecondaryBrush, 11.0, bold: true);
        ry += 16.0;
        DrawText(context, "Feedback loop transmission, sensitivity Sa^T(s), and steady-state error analysis", rx, ry, tokens.TextMutedBrush, 8.5);
        ry += 16.0;

        // Given System Card
        ry = DrawCard(context, rx, ry, cardW, 26.0, tokens);
        DrawText(context, $"Given System:   Plant P(s) = 1 / (s + {Problem5PoleA:F2})   |   Controller C(s) = K = {Problem5GainK:F2}", rx + 8.0, ry - 20.0, tokens.TextBrush, 9.0, bold: true);
        ry += 6.0;

        // Step 1: Closed-Loop Loop Transmission
        double rStep1H = 56.0;
        ry = DrawCard(context, rx, ry, cardW, rStep1H, tokens);
        double rc1Y = ry - rStep1H + 6.0;
        DrawText(context, "Step 1 • Closed-Loop Transfer Function & Dominant Pole:", rx + 8.0, rc1Y, tokens.TextMutedBrush, 8.5);
        rc1Y += 14.0;

        DrawMathFraction(
            context,
            prefix: "T(s) = ",
            num: "K",
            den: "s + a + K",
            suffix: $" = {Problem5GainK:F2} / (s + {(Problem5PoleA + Problem5GainK):F2})",
            x: rx + 12.0,
            y: rc1Y,
            textBrush: tokens.TextBrush,
            barBrush: tokens.SecondaryBrush,
            fontSize: 9.0,
            suffixBrush: tokens.TextBrush,
            boldSuffix: true,
            secondarySuffix: $"   ⟹  Pole: s = -(a + K) = {-p5.ClosedLoopPoleReal:F2}",
            secondaryBrush: SuccessBrush
        );
        ry += 6.0;

        // Step 2: Bode Sensitivity Function
        double rStep2H = 74.0;
        ry = DrawCard(context, rx, ry, cardW, rStep2H, tokens);
        double rc2Y = ry - rStep2H + 6.0;
        DrawText(context, "Step 2 • Parameter Sensitivity Function Sa^T(s) with respect to Plant Pole 'a':", rx + 8.0, rc2Y, tokens.TextMutedBrush, 8.5);
        rc2Y += 14.0;

        DrawMathFraction(
            context,
            prefix: "Sa^T(s) ≜ (a / T) · (∂T / ∂a) = ",
            num: "-a",
            den: "s + a + K",
            suffix: "",
            x: rx + 12.0,
            y: rc2Y,
            textBrush: tokens.TextBrush,
            barBrush: tokens.SecondaryBrush,
            fontSize: 9.0
        );
        rc2Y += 28.0;

        var sensBoxFill = tokens.IsDark
            ? new SolidColorBrush(Color.FromArgb(35, 245, 158, 11))
            : new SolidColorBrush(Color.FromArgb(25, 245, 158, 11));
        var sensBox = DrawMathBox(
            context,
            $"DC Sensitivity Magnitude |Sa^T(0)| = a / (a + K) = {p5.LowFrequencySensitivity:F4}",
            rx + 12.0,
            rc2Y,
            tokens.WarningBrush,
            tokens.WarningBrush,
            sensBoxFill,
            9.0
        );
        double sensDb = 20.0 * Math.Log10(Math.Max(p5.LowFrequencySensitivity, 1e-9));
        DrawText(context, $"({sensDb:F1} dB attenuation)", sensBox.Right + 8.0, rc2Y + 4.0, tokens.TextDimBrush, 8.5);
        ry += 6.0;

        // Step 3: Steady-State Tracking & Disturbance Errors
        double rStep3H = 88.0;
        ry = DrawCard(context, rx, ry, cardW, rStep3H, tokens);
        double rc3Y = ry - rStep3H + 6.0;
        DrawText(context, "Step 3 • Steady-State Errors to Canonical Standard Inputs:", rx + 8.0, rc3Y, tokens.TextMutedBrush, 8.5);
        rc3Y += 15.0;

        // 1) Unit Step Ref
        DrawMathFraction(
            context,
            prefix: "1) Unit Step Ref r(t) = 1(t):   ess = ",
            num: "a",
            den: "a + K",
            suffix: $" = {p5.SteadyStateErrorStep:F4}",
            x: rx + 12.0,
            y: rc3Y,
            textBrush: tokens.TextBrush,
            barBrush: tokens.TextMutedBrush,
            fontSize: 8.5,
            suffixBrush: tokens.TextBrush,
            boldSuffix: true
        );
        rc3Y += 26.0;

        // 2) Unit Ramp Ref
        DrawText(context, "2) Unit Ramp Ref r(t) = t:   ess = ∞", rx + 12.0, rc3Y, tokens.DangerBrush, 8.5, bold: true);
        DrawText(context, "  (Type 0 system has finite step error and cannot track ramp without integrator)", rx + 185.0, rc3Y, tokens.TextMutedBrush, 8.0);
        rc3Y += 16.0;

        // 3) Unit Disturbance
        DrawMathFraction(
            context,
            prefix: "3) Unit Disturbance d(t) = 1(t):   ess = ",
            num: "1",
            den: "a + K",
            suffix: $" = {p5.SteadyStateErrorDisturbance:F4}",
            x: rx + 12.0,
            y: rc3Y,
            textBrush: tokens.TextBrush,
            barBrush: tokens.AccentBrush,
            fontSize: 8.5,
            suffixBrush: tokens.AccentBrush,
            boldSuffix: true
        );
    }
    #endregion

    #region Helper Drawing Primitives & Academic Math Renderers
    private void DrawPolyline(DrawingContext context, IList<Point> points, IBrush stroke, double thickness, IDashStyle? dashStyle = null)
    {
        if (points.Count < 2) return;

        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            ctx.BeginFigure(points[0], false);
            for (int i = 1; i < points.Count; i++)
            {
                ctx.LineTo(points[i]);
            }
            ctx.EndFigure(false);
        }

        var pen = new Pen(stroke, thickness, dashStyle);
        context.DrawGeometry(null, pen, geom);
    }

    private void DrawText(DrawingContext context, string text, double x, double y, IBrush brush, double size = 9.0, bool bold = false, bool italic = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        var style = italic ? FontStyle.Italic : FontStyle.Normal;
        var weight = bold ? FontWeight.Bold : FontWeight.Normal;
        var ft = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI, sans-serif", style, weight),
            size,
            brush
        );
        context.DrawText(ft, new Point(x, y));
    }

    private void DrawBadge(DrawingContext context, string text, double x, double y, IBrush accentBrush)
    {
        var ft = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Bold),
            8.0,
            accentBrush
        );

        double padH = 6.0;
        double padV = 2.0;
        var rect = new Rect(x, y, ft.Width + padH * 2.0, ft.Height + padV * 2.0);

        var bg = new SolidColorBrush(Color.FromArgb(30, 100, 150, 240));
        context.DrawRectangle(bg, new Pen(accentBrush, 1.0), rect, 3, 3);
        context.DrawText(ft, new Point(x + padH, y + padV));
    }

    private double DrawCard(DrawingContext context, double x, double y, double width, double height, CircuitThemeTokens tokens)
    {
        var rect = new Rect(x, y, width, height);
        var fill = tokens.IsDark
            ? new SolidColorBrush(Color.FromArgb(35, 30, 41, 59))
            : new SolidColorBrush(Color.FromArgb(30, 226, 232, 240));
        context.DrawRectangle(fill, new Pen(tokens.BorderSubtleBrush, 1.0), rect, 6, 6);
        return y + height;
    }

    private Rect DrawMathFraction(
        DrawingContext context,
        string prefix,
        string num,
        string den,
        string suffix,
        double x,
        double y,
        IBrush textBrush,
        IBrush barBrush,
        double fontSize = 9.0,
        IBrush? suffixBrush = null,
        bool boldSuffix = false,
        string secondarySuffix = "",
        IBrush? secondaryBrush = null)
    {
        var fontRegular = new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Normal);
        var fontBold = new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Bold);

        FormattedText? ftPrefix = !string.IsNullOrEmpty(prefix)
            ? new FormattedText(prefix, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, fontBold, fontSize, textBrush)
            : null;

        double subFontSize = fontSize * 0.95;
        var ftNum = new FormattedText(num, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, fontRegular, subFontSize, textBrush);
        var ftDen = new FormattedText(den, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, fontRegular, subFontSize, textBrush);

        FormattedText? ftSuffix = !string.IsNullOrEmpty(suffix)
            ? new FormattedText(suffix, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, boldSuffix ? fontBold : fontRegular, fontSize, suffixBrush ?? textBrush)
            : null;

        FormattedText? ftSec = !string.IsNullOrEmpty(secondarySuffix)
            ? new FormattedText(secondarySuffix, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, fontRegular, fontSize * 0.9, secondaryBrush ?? textBrush)
            : null;

        double prefixW = ftPrefix?.Width ?? 0.0;
        double fracW = Math.Max(ftNum.Width, ftDen.Width) + 8.0;
        double barY = y + ftNum.Height + 1.0;

        // Draw prefix centered vertically with fraction bar
        if (ftPrefix != null)
        {
            context.DrawText(ftPrefix, new Point(x, barY - ftPrefix.Height / 2.0));
        }

        // Draw horizontal fraction bar
        double fracX = x + prefixW + (prefixW > 0 ? 3.0 : 0.0);
        var barPen = new Pen(barBrush, 1.2);
        context.DrawLine(barPen, new Point(fracX, barY), new Point(fracX + fracW, barY));

        // Draw Numerator (centered above bar)
        double numX = fracX + (fracW - ftNum.Width) / 2.0;
        context.DrawText(ftNum, new Point(numX, barY - ftNum.Height - 1.5));

        // Draw Denominator (centered below bar)
        double denX = fracX + (fracW - ftDen.Width) / 2.0;
        context.DrawText(ftDen, new Point(denX, barY + 2.0));

        // Draw Suffix
        double curX = fracX + fracW + 4.0;
        if (ftSuffix != null)
        {
            context.DrawText(ftSuffix, new Point(curX, barY - ftSuffix.Height / 2.0));
            curX += ftSuffix.Width + 4.0;
        }

        // Draw Secondary Suffix
        if (ftSec != null)
        {
            context.DrawText(ftSec, new Point(curX, barY - ftSec.Height / 2.0));
            curX += ftSec.Width;
        }

        return new Rect(x, y, curX - x, ftNum.Height + ftDen.Height + 5.0);
    }

    private Rect DrawMathBox(
        DrawingContext context,
        string text,
        double x,
        double y,
        IBrush textBrush,
        IBrush borderBrush,
        IBrush fillBrush,
        double fontSize = 9.5)
    {
        var ft = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Bold),
            fontSize,
            textBrush
        );

        double padH = 8.0;
        double padV = 3.5;
        var rect = new Rect(x, y, ft.Width + padH * 2.0, ft.Height + padV * 2.0);

        context.DrawRectangle(fillBrush, new Pen(borderBrush, 1.2), rect, 4, 4);
        context.DrawText(ft, new Point(x + padH, y + padV));

        return rect;
    }
    #endregion
}
