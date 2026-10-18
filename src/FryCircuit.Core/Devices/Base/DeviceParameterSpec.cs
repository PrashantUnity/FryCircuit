using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Base;

public class DeviceParameterSpec
{
    public string ParameterName { get; set; }
    public string DisplayLabel { get; set; }
    public string Unit { get; set; }
    public double Minimum { get; set; }
    public double Maximum { get; set; }
    public double DefaultValue { get; set; }
    public double Step { get; set; }

    public Func<Device, double> Getter { get; set; }
    public Action<Device, double> Setter { get; set; }

    public DeviceParameterSpec(
        string parameterName,
        string displayLabel,
        string unit,
        double min,
        double max,
        double def,
        double step,
        Func<Device, double> getter,
        Action<Device, double> setter)
    {
        ParameterName = parameterName;
        DisplayLabel = displayLabel;
        Unit = unit;
        Minimum = min;
        Maximum = max;
        DefaultValue = def;
        Step = step;
        Getter = getter;
        Setter = setter;
    }
}
