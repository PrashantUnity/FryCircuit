using CommunityToolkit.Mvvm.ComponentModel;

namespace FryCircuit.UI.ViewModels;

public partial class ParameterScaleOption : ObservableObject
{
    public string Prefix { get; }
    public string Unit { get; }
    public string Label => $"{Prefix}{Unit}";
    public double Multiplier { get; }
    public string ToolTip { get; }
    public double DefaultMin { get; }
    public double DefaultMax { get; }
    public double Step { get; }

    [ObservableProperty]
    private bool _isActive;

    public ParameterScaleOption(
        string prefix,
        string unit,
        double multiplier,
        string toolTip,
        double defaultMin = 0.1,
        double defaultMax = 1000.0,
        double step = 0.1)
    {
        Prefix = prefix;
        Unit = unit;
        Multiplier = multiplier;
        ToolTip = toolTip;
        DefaultMin = defaultMin;
        DefaultMax = defaultMax;
        Step = step;
    }
}
