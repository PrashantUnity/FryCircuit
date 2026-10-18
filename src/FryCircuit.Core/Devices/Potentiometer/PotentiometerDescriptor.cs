using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Potentiometer;

public class PotentiometerDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "potentiometer";
    public override string DisplayName => "Potentiometer";
    public override string Category => "Passives";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "A", 0, -1, PinDirection.Up),
        new(1, "B", 0, 1, PinDirection.Down),
        new(2, "Wiper", 1, 0, PinDirection.Right)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var pot = new PotentiometerDevice(id, x, y);
        pot.SetupDefaultPins();
        return pot;
    }

    public override string FormatLabel(Device device)
    {
        return device is PotentiometerDevice pot
            ? $"{FormatEngineering(pot.Resistance, "Ω")} ({(int)(pot.Wiper * 100)}%)"
            : string.Empty;
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Resistance",
        "Total Resistance",
        "Ω",
        min: 10.0,
        max: 10e6,
        def: 10000.0,
        step: 100.0,
        getter: d => d is PotentiometerDevice p ? p.Resistance : 10000.0,
        setter: (d, v) => { if (d is PotentiometerDevice p) p.Resistance = Math.Max(1.0, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "Wiper",
            "Wiper Position (Ratio)",
            "%",
            min: 0.01,
            max: 0.99,
            def: 0.5,
            step: 0.01,
            getter: d => d is PotentiometerDevice p ? p.Wiper : 0.5,
            setter: (d, v) => { if (d is PotentiometerDevice p) p.Wiper = Math.Clamp(v, 0.001, 0.999); }
        )
    };
}
