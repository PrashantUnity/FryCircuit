using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Capacitor;

public class CapacitorDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "capacitor";
    public override string DisplayName => "Capacitor";
    public override string Category => "Passives";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "A", 0, -1, PinDirection.Up),
        new(1, "B", 0, 1, PinDirection.Down)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var cap = new CapacitorDevice(id, x, y);
        cap.SetupDefaultPins();
        return cap;
    }

    public override string FormatLabel(Device device)
    {
        return device is CapacitorDevice cap ? FormatEngineering(cap.Capacitance, "F") : string.Empty;
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Capacitance",
        "Capacitance",
        "F",
        min: 1e-15,
        max: 1.0,
        def: 1e-6,
        step: 1e-7,
        getter: d => d is CapacitorDevice c ? c.Capacitance : 1e-6,
        setter: (d, v) => { if (d is CapacitorDevice c) c.Capacitance = Math.Max(1e-16, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "ESR",
            "Equivalent Series Res (ESR)",
            "Ω",
            min: 0.0001,
            max: 10.0,
            def: 0.01,
            step: 0.005,
            getter: d => d.GetParameter("esr", 0.01),
            setter: (d, v) => d.SetParameter("esr", Math.Max(1e-6, v))
        ),
        new(
            "StateVoltage",
            "Pre-charge Voltage",
            "V",
            min: -1000.0,
            max: 1000.0,
            def: 0.0,
            step: 0.5,
            getter: d => d is CapacitorDevice c ? c.StateVoltage : 0.0,
            setter: (d, v) => { if (d is CapacitorDevice c) c.StateVoltage = v; }
        )
    };
}
