using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.OpAmp;

public class OpAmpDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "opamp";
    public override string DisplayName => "Operational Amplifier";
    public override string Category => "Active";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Inverting (-)", -1, -1, PinDirection.Left),
        new(1, "NonInverting (+)", -1, 1, PinDirection.Left),
        new(2, "Output", 1, 0, PinDirection.Right)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.2, 3.0);

    public override Device CreateInstance(int id, double x, double y)
    {
        var op = new OpAmpDevice(id, x, y);
        op.SetupDefaultPins();
        return op;
    }

    public override string FormatLabel(Device device) => string.Empty;

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Gain",
        "Open-Loop Gain (Aol)",
        "V/V",
        min: 10.0,
        max: 1e7,
        def: 100000.0,
        step: 1000.0,
        getter: d => d is OpAmpDevice op ? op.Gain : 100000.0,
        setter: (d, v) => { if (d is OpAmpDevice op) op.Gain = Math.Max(1.0, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "MaxVoltage",
            "Positive Supply Limit (V+)",
            "V",
            min: 1.0,
            max: 50.0,
            def: 15.0,
            step: 0.5,
            getter: d => d is OpAmpDevice op ? op.MaxVoltage : 15.0,
            setter: (d, v) => { if (d is OpAmpDevice op) op.MaxVoltage = v; }
        ),
        new(
            "MinVoltage",
            "Negative Supply Limit (V-)",
            "V",
            min: -50.0,
            max: -1.0,
            def: -15.0,
            step: 0.5,
            getter: d => d is OpAmpDevice op ? op.MinVoltage : -15.0,
            setter: (d, v) => { if (d is OpAmpDevice op) op.MinVoltage = v; }
        )
    };
}
