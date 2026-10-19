using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Lamp;

public class LampDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "lamp";
    public override string DisplayName => "Incandescent Lamp";
    public override string Category => "Passives";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "A", 0, -1, PinDirection.Up),
        new(1, "B", 0, 1, PinDirection.Down)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var lamp = new LampDevice(id, x, y);
        lamp.SetupDefaultPins();
        return lamp;
    }

    public override string FormatLabel(Device device)
    {
        return device is LampDevice lamp
            ? $"{lamp.NominalPower:F0}W {lamp.NominalVoltage:F0}V"
            : "60W 12V";
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "NominalPower",
        "Power Rating",
        "W",
        min: 1.0,
        max: 500.0,
        def: 60.0,
        step: 5.0,
        getter: d => d is LampDevice l ? l.NominalPower : 60.0,
        setter: (d, v) => { if (d is LampDevice l) l.NominalPower = Math.Max(0.5, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "NominalVoltage",
            "Nominal Voltage",
            "V",
            min: 1.0,
            max: 240.0,
            def: 12.0,
            step: 1.0,
            getter: d => d is LampDevice l ? l.NominalVoltage : 12.0,
            setter: (d, v) => { if (d is LampDevice l) l.NominalVoltage = Math.Max(0.1, v); }
        ),
        new(
            "MaxVoltage",
            "Max Voltage (Burnout)",
            "V",
            min: 1.0,
            max: 300.0,
            def: 18.0,
            step: 1.0,
            getter: d => d is LampDevice l ? l.MaxVoltage : 18.0,
            setter: (d, v) => { if (d is LampDevice l) l.MaxVoltage = Math.Max(0.5, v); }
        )
    };
}
