using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FryCircuit.UI.Theming;

namespace FryCircuit.UI.Controls;

public enum ChannelCoupling
{
    DC,
    AC,
    Ground
}

public enum OscilloscopeDisplayLayout
{
    Overlay,
    Stacked,
    XY,
    FftSpectrum
}

public enum TriggerMode
{
    Auto,
    Normal,
    Single
}

public enum TriggerSlope
{
    Rising,
    Falling
}

public partial class OscilloscopeTriggerSettings : ObservableObject
{
    [ObservableProperty]
    private int _sourceChannelIndex = 1;

    [ObservableProperty]
    private TriggerMode _mode = TriggerMode.Auto;

    [ObservableProperty]
    private TriggerSlope _slope = TriggerSlope.Rising;

    [ObservableProperty]
    private double _level = 0.0; // In Volts

    [ObservableProperty]
    private bool _isArmed = true;

    [ObservableProperty]
    private bool _isTriggered = false;
}

public partial class OscilloscopeTrace : ObservableObject
{
    public int ChannelIndex { get; set; } = 1;

    [ObservableProperty]
    private string _name = "Trace";

    [ObservableProperty]
    private Color _color = Color.FromRgb(245, 158, 11);

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private int? _targetNodeId;

    [ObservableProperty]
    private int? _referenceNodeId; // For differential probing V(A) - V(B)

    [ObservableProperty]
    private ChannelCoupling _coupling = ChannelCoupling.DC;

    [ObservableProperty]
    private double _voltsPerDiv = 1.0;

    [ObservableProperty]
    private double _verticalOffsetDivs = 0.0; // In vertical divisions (-4.0 to +4.0)

    [ObservableProperty]
    private string _unit = "V";

    public Queue<double> Samples { get; } = new();
    public int MaxSamples { get; set; } = 600;

    public double CurrentValue => Samples.Count > 0 ? Samples.Last() : 0.0;

    public virtual void AddSample(double value)
    {
        if (Coupling == ChannelCoupling.Ground)
        {
            value = 0.0;
        }

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

public class OscilloscopeChannel : OscilloscopeTrace
{
    public OscilloscopeChannel()
    {
    }

    public OscilloscopeChannel(int index, string name, Color color, string unit = "V")
    {
        ChannelIndex = index;
        Name = name;
        Color = color;
        Unit = unit;
    }

    public static OscilloscopeChannel CreateDefault(int index)
    {
        var color = CircuitThemeTokens.GetDefaultChannelColor(index);
        return new OscilloscopeChannel(index, $"CH{index}", color, "V");
    }
}
