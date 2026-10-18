using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace FryCircuit.UI.Controls;

public class RotaryKnobControl : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<RotaryKnobControl, double>(nameof(Value), 1000.0, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<RotaryKnobControl, double>(nameof(Minimum), 1.0);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<RotaryKnobControl, double>(nameof(Maximum), 1000000.0);

    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<RotaryKnobControl, string>(nameof(Unit), "Ω");

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<RotaryKnobControl, string>(nameof(Label), "Resistance");

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public string Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    private bool _isDragging;
    private Point _lastPos;

    static RotaryKnobControl()
    {
        AffectsRender<RotaryKnobControl>(ValueProperty, LabelProperty, UnitProperty);
    }

    public RotaryKnobControl()
    {
        ClipToBounds = false;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _lastPos = e.GetPosition(this);
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_isDragging)
        {
            var curPos = e.GetPosition(this);
            double deltaY = _lastPos.Y - curPos.Y;
            _lastPos = curPos;

            bool isLog = Minimum > 0 && (Maximum / Minimum) > 20.0;
            if (isLog)
            {
                double logMin = Math.Log10(Minimum);
                double logMax = Math.Log10(Maximum);
                double currentLog = Math.Log10(Math.Clamp(Value, Minimum, Maximum));
                double step = (logMax - logMin) * 0.01 * deltaY;
                double newLog = Math.Clamp(currentLog + step, logMin, logMax);
                Value = Math.Pow(10.0, newLog);
            }
            else
            {
                double range = Math.Max(1e-9, Maximum - Minimum);
                double step = range * 0.005 * deltaY;
                Value = Math.Clamp(Value + step, Minimum, Maximum);
            }

            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isDragging)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        bool isLog = Minimum > 0 && (Maximum / Minimum) > 20.0;
        if (isLog)
        {
            double logMin = Math.Log10(Minimum);
            double logMax = Math.Log10(Maximum);
            double currentLog = Math.Log10(Math.Clamp(Value, Minimum, Maximum));
            double step = (logMax - logMin) * 0.05 * e.Delta.Y;
            double newLog = Math.Clamp(currentLog + step, logMin, logMax);
            Value = Math.Pow(10.0, newLog);
        }
        else
        {
            double range = Math.Max(1e-9, Maximum - Minimum);
            double step = range * 0.02 * e.Delta.Y;
            Value = Math.Clamp(Value + step, Minimum, Maximum);
        }
        InvalidateVisual();
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double w = Bounds.Width;
        double h = Bounds.Height;
        double size = Math.Min(w, h);
        if (size <= 10) return;

        var center = new Point(w / 2.0, (h / 2.0) - 5);
        double radius = (size / 2.0) - 15;

        // Calculate angle for value: 135 deg to 405 deg (270 deg sweep)
        bool isLog = Minimum > 0 && (Maximum / Minimum) > 20.0;
        double fraction;
        if (isLog)
        {
            double logMin = Math.Log10(Minimum);
            double logMax = Math.Log10(Maximum);
            double currentLog = Math.Log10(Math.Clamp(Value, Minimum, Maximum));
            fraction = (logMax > logMin) ? Math.Clamp((currentLog - logMin) / (logMax - logMin), 0.0, 1.0) : 0.0;
        }
        else
        {
            double range = Math.Max(1e-9, Maximum - Minimum);
            fraction = Math.Clamp((Value - Minimum) / range, 0.0, 1.0);
        }

        double startAngle = 135.0;
        double sweepAngle = fraction * 270.0;
        double currentAngle = startAngle + sweepAngle;
        double rad = currentAngle * Math.PI / 180.0;

        // 1. Knurled calibration ticks around perimeter
        var notchPen = new Pen(new SolidColorBrush(Color.FromArgb(90, 100, 130, 160)), 1);
        for (int i = 0; i <= 18; i++)
        {
            double notchAngle = (135.0 + i * (270.0 / 18.0)) * Math.PI / 180.0;
            double r1 = radius + 3;
            double r2 = radius + 6;
            context.DrawLine(notchPen, 
                new Point(center.X + r1 * Math.Cos(notchAngle), center.Y + r1 * Math.Sin(notchAngle)),
                new Point(center.X + r2 * Math.Cos(notchAngle), center.Y + r2 * Math.Sin(notchAngle)));
        }

        // 2. Outer track background arc
        var trackPen = new Pen(new SolidColorBrush(Color.FromRgb(26, 34, 46)), 5)
        {
            LineCap = PenLineCap.Round
        };
        var trackGeom = new StreamGeometry();
        using (var ctx = trackGeom.Open())
        {
            double startRad = 135.0 * Math.PI / 180.0;
            double endRad = 405.0 * Math.PI / 180.0;
            ctx.BeginFigure(new Point(center.X + radius * Math.Cos(startRad), center.Y + radius * Math.Sin(startRad)), false);
            ctx.ArcTo(new Point(center.X + radius * Math.Cos(endRad), center.Y + radius * Math.Sin(endRad)),
                new Size(radius, radius), 0.0, true, SweepDirection.Clockwise);
        }
        context.DrawGeometry(null, trackPen, trackGeom);

        // 3. Glowing active progress arc
        if (sweepAngle > 2.0)
        {
            var activeArcPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 230, 118)), 5)
            {
                LineCap = PenLineCap.Round
            };
            var activeArcGeom = new StreamGeometry();
            using (var ctx = activeArcGeom.Open())
            {
                double startRad = 135.0 * Math.PI / 180.0;
                ctx.BeginFigure(new Point(center.X + radius * Math.Cos(startRad), center.Y + radius * Math.Sin(startRad)), false);
                ctx.ArcTo(new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad)),
                    new Size(radius, radius), 0.0, sweepAngle > 180.0, SweepDirection.Clockwise);
            }
            context.DrawGeometry(null, activeArcPen, activeArcGeom);
        }

        // 4. Knob inner metallic body
        var knobBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(42, 52, 70), 0.0),
                new GradientStop(Color.FromRgb(20, 25, 34), 1.0)
            }
        };
        var knobBorder = new Pen(new SolidColorBrush(Color.FromRgb(65, 80, 105)), 1.5);
        context.DrawEllipse(knobBrush, knobBorder, center, radius - 6, radius - 6);

        // 5. Active indicator dot on knob perimeter
        double indX = center.X + (radius - 11) * Math.Cos(rad);
        double indY = center.Y + (radius - 11) * Math.Sin(rad);
        var activeBrush = new SolidColorBrush(Color.FromRgb(0, 255, 136));
        context.DrawEllipse(activeBrush, null, new Point(indX, indY), 3.5, 3.5);

        // 6. Formatted value string in center
        string formattedValue = FormatValue(Value, Unit);
        var valueText = new FormattedText(
            formattedValue,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            12.5,
            Brushes.White
        );
        context.DrawText(valueText, new Point(center.X - valueText.Width / 2.0, center.Y - valueText.Height / 2.0));

        // Label below
        if (!string.IsNullOrWhiteSpace(Label))
        {
            var labelText = new FormattedText(
                Label,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
                10,
                new SolidColorBrush(Color.FromRgb(140, 155, 175))
            );
            context.DrawText(labelText, new Point(center.X - labelText.Width / 2.0, center.Y + radius + 3));
        }
    }

    private static string FormatValue(double val, string unit)
    {
        double abs = Math.Abs(val);
        if (abs >= 1e6) return $"{val / 1e6:F2} M{unit}";
        if (abs >= 1e3) return $"{val / 1e3:F2} k{unit}";
        if (abs >= 1.0) return $"{val:F2} {unit}";
        if (abs >= 1e-3) return $"{val * 1e3:F2} m{unit}";
        if (abs >= 1e-6) return $"{val * 1e6:F2} μ{unit}";
        if (abs >= 1e-9) return $"{val * 1e9:F2} n{unit}";
        if (abs >= 1e-12) return $"{val * 1e12:F2} p{unit}";
        if (abs >= 1e-15) return $"{val * 1e15:F2} f{unit}";
        if (abs >= 1e-18) return $"{val * 1e18:F2} a{unit}";
        return $"{val:G3} {unit}";
    }
}
