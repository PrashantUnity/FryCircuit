using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Resistor;

public class ResistorDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "resistor";
    public override string DisplayName => "Resistor";
    public override string Category => "Passives";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "A", 0, -1, PinDirection.Up),
        new(1, "B", 0, 1, PinDirection.Down)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var res = new ResistorDevice(id, x, y);
        res.SetupDefaultPins();
        return res;
    }

    public override string FormatLabel(Device device)
    {
        return device is ResistorDevice res ? FormatEngineering(res.Resistance, "Ω") : string.Empty;
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Resistance",
        "Resistance",
        "Ω",
        min: 0.1,
        max: 100e6,
        def: 1000.0,
        step: 10.0,
        getter: d => d is ResistorDevice r ? r.Resistance : 1000.0,
        setter: (d, v) => { if (d is ResistorDevice r) r.Resistance = Math.Max(0.01, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "PowerRating",
            "Power Rating",
            "W",
            min: 0.125,
            max: 50.0,
            def: 0.25,
            step: 0.125,
            getter: d => d.GetParameter("power_rating", 0.25),
            setter: (d, v) => d.SetParameter("power_rating", Math.Max(0.05, v))
        ),
        new(
            "Tolerance",
            "Tolerance (±)",
            "%",
            min: 0.1,
            max: 20.0,
            def: 1.0,
            step: 0.5,
            getter: d => d.GetParameter("tolerance", 1.0),
            setter: (d, v) => d.SetParameter("tolerance", Math.Max(0.01, v))
        )
    };
}
