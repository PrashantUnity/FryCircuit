using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace FryCircuit.UI.Theming;

/// <summary>
/// Attached property helper allowing controls to dynamically bind brushes by token resource key.
/// Example: <c>theming:ThemeBrush.Foreground="CircuitAccentBrush"</c> or
/// <c>theming:ThemeBrush.Foreground="{Binding AccentKey}"</c>.
/// </summary>
public sealed class ThemeBrush
{
    public static readonly AttachedProperty<string?> ForegroundProperty =
        AvaloniaProperty.RegisterAttached<ThemeBrush, Control, string?>("Foreground");

    public static readonly AttachedProperty<string?> BackgroundProperty =
        AvaloniaProperty.RegisterAttached<ThemeBrush, Control, string?>("Background");

    public static readonly AttachedProperty<string?> BorderBrushProperty =
        AvaloniaProperty.RegisterAttached<ThemeBrush, Control, string?>("BorderBrush");

    private static readonly AttachedProperty<IDisposable?> ForegroundSubscriptionProperty =
        AvaloniaProperty.RegisterAttached<ThemeBrush, Control, IDisposable?>("ForegroundSubscription");

    private static readonly AttachedProperty<IDisposable?> BackgroundSubscriptionProperty =
        AvaloniaProperty.RegisterAttached<ThemeBrush, Control, IDisposable?>("BackgroundSubscription");

    private static readonly AttachedProperty<IDisposable?> BorderBrushSubscriptionProperty =
        AvaloniaProperty.RegisterAttached<ThemeBrush, Control, IDisposable?>("BorderBrushSubscription");

    static ThemeBrush()
    {
        ForegroundProperty.Changed.AddClassHandler<Control>((c, e) => OnKeyChanged(c, e, TextElement.ForegroundProperty, ForegroundSubscriptionProperty));
        BackgroundProperty.Changed.AddClassHandler<Control>((c, e) => OnKeyChanged(c, e, Border.BackgroundProperty, BackgroundSubscriptionProperty));
        BorderBrushProperty.Changed.AddClassHandler<Control>((c, e) => OnKeyChanged(c, e, Border.BorderBrushProperty, BorderBrushSubscriptionProperty));
    }

    private ThemeBrush() { }

    public static string? GetForeground(Control control) => control.GetValue(ForegroundProperty);
    public static void SetForeground(Control control, string? key) => control.SetValue(ForegroundProperty, key);

    public static string? GetBackground(Control control) => control.GetValue(BackgroundProperty);
    public static void SetBackground(Control control, string? key) => control.SetValue(BackgroundProperty, key);

    public static string? GetBorderBrush(Control control) => control.GetValue(BorderBrushProperty);
    public static void SetBorderBrush(Control control, string? key) => control.SetValue(BorderBrushProperty, key);

    private static void OnKeyChanged(Control control, AvaloniaPropertyChangedEventArgs e, AvaloniaProperty target, AttachedProperty<IDisposable?> subscriptionProperty)
    {
        control.GetValue(subscriptionProperty)?.Dispose();
        var key = e.GetNewValue<string?>();
        IDisposable? subscription = null;
        if (string.IsNullOrEmpty(key))
        {
            control.ClearValue(target);
        }
        else
        {
            subscription = control.GetResourceObservable(key).Subscribe(new BrushSetter(control, target));
        }
        control.SetValue(subscriptionProperty, subscription);
    }

    private sealed class BrushSetter(Control control, AvaloniaProperty target) : IObserver<object?>
    {
        public void OnNext(object? value)
        {
            if (value is IBrush brush) control.SetValue(target, brush);
            else control.ClearValue(target);
        }

        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }
}
