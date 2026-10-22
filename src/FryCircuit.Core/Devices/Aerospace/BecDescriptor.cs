using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Aerospace;

public class BecDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "bec";
    public override string DisplayName => "DC-DC Step-Down BEC";
    public override string Category => "Drones & Aero";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "VIN (+)", -1, -1, PinDirection.Left),
        new(1, "GND (-)", -1, 1, PinDirection.Left),
        new(2, "VOUT (+)", 1, 0, PinDirection.Right)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.4, 2.4);

    public override Device CreateInstance(int id, double x, double y)
    {
        var bec = new BecDevice(id, x, y);
        bec.SetupDefaultPins();
        return bec;
    }

    public override string FormatLabel(Device device)
    {
        return device is BecDevice b
            ? $"{b.OutputVoltage:F1}V ({b.LoadCurrent:F2}A)"
            : string.Empty;
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "OutputVoltage",
        "Output Voltage",
        "V",
        min: 1.2,
        max: 24.0,
        def: 5.0,
        step: 0.1,
        getter: d => d is BecDevice b ? b.OutputVoltage : 5.0,
        setter: (d, v) => { if (d is BecDevice b) b.OutputVoltage = Math.Max(0.5, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "MaxOutputCurrent",
            "Max Current",
            "A",
            min: 0.5,
            max: 20.0,
            def: 3.0,
            step: 0.5,
            getter: d => d is BecDevice b ? b.MaxOutputCurrent : 3.0,
            setter: (d, v) => { if (d is BecDevice b) b.MaxOutputCurrent = Math.Max(0.1, v); }
        ),
        new(
            "Efficiency",
            "Efficiency",
            "%",
            min: 0.50,
            max: 0.99,
            def: 0.92,
            step: 0.01,
            getter: d => d is BecDevice b ? b.Efficiency : 0.92,
            setter: (d, v) => { if (d is BecDevice b) b.Efficiency = Math.Clamp(v, 0.50, 0.99); }
        ),
        new(
            "DropoutVoltage",
            "Dropout Voltage",
            "V",
            min: 0.1,
            max: 3.0,
            def: 0.8,
            step: 0.1,
            getter: d => d is BecDevice b ? b.DropoutVoltage : 0.8,
            setter: (d, v) => { if (d is BecDevice b) b.DropoutVoltage = Math.Max(0.05, v); }
        )
    };
}
