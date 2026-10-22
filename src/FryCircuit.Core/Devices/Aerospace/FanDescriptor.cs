using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Aerospace;

public class FanDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "fan";
    public override string DisplayName => "DC Cooling Fan";
    public override string Category => "Drones & Aero";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "+", -1, 0, PinDirection.Left),
        new(1, "-", 1, 0, PinDirection.Right),
        new(2, "TACH", 0, 1, PinDirection.Down)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.4, 2.4);

    public override Device CreateInstance(int id, double x, double y)
    {
        var fan = new FanDevice(id, x, y);
        fan.SetupDefaultPins();
        return fan;
    }

    public override string FormatLabel(Device device)
    {
        return device is FanDevice fan
            ? $"{fan.CurrentRpm:F0} RPM ({fan.CurrentAirflowCfm:F1} CFM)"
            : string.Empty;
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "RatedVoltage",
        "Rated Voltage",
        "V",
        min: 1.0,
        max: 48.0,
        def: 12.0,
        step: 1.0,
        getter: d => d is FanDevice f ? f.RatedVoltage : 12.0,
        setter: (d, v) => { if (d is FanDevice f) f.RatedVoltage = Math.Max(0.5, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "MaxRpm",
            "Max Speed",
            "RPM",
            min: 500.0,
            max: 30000.0,
            def: 5000.0,
            step: 500.0,
            getter: d => d is FanDevice f ? f.MaxRpm : 5000.0,
            setter: (d, v) => { if (d is FanDevice f) f.MaxRpm = Math.Max(100.0, v); }
        ),
        new(
            "RatedCurrent",
            "Rated Current",
            "A",
            min: 0.02,
            max: 10.0,
            def: 0.25,
            step: 0.05,
            getter: d => d is FanDevice f ? f.RatedCurrent : 0.25,
            setter: (d, v) => { if (d is FanDevice f) f.RatedCurrent = Math.Max(0.01, v); }
        ),
        new(
            "AirflowRatingCfm",
            "Airflow Rating",
            "CFM",
            min: 1.0,
            max: 300.0,
            def: 35.0,
            step: 5.0,
            getter: d => d is FanDevice f ? f.AirflowRatingCfm : 35.0,
            setter: (d, v) => { if (d is FanDevice f) f.AirflowRatingCfm = Math.Max(0.1, v); }
        ),
        new(
            "StartVoltage",
            "Start Voltage",
            "V",
            min: 0.5,
            max: 24.0,
            def: 4.0,
            step: 0.5,
            getter: d => d is FanDevice f ? f.StartVoltage : 4.0,
            setter: (d, v) => { if (d is FanDevice f) f.StartVoltage = Math.Max(0.1, v); }
        )
    };
}
