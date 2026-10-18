using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.TransmissionLine;

public class TransmissionLineDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "tline";
    public override string DisplayName => "Transmission Line";
    public override string Category => "RF & Microwave";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Port1 (+)", -2, -1.0, PinDirection.Left),
        new(1, "Port1 (-)", -2, 1.0, PinDirection.Left),
        new(2, "Port2 (+)", 2, -1.0, PinDirection.Right),
        new(3, "Port2 (-)", 2, 1.0, PinDirection.Right)
    };

    public override (double Width, double Height) BoundingBoxSize => (4.2, 2.0);

    public override Device CreateInstance(int id, double x, double y)
    {
        var tl = new TransmissionLineDevice(id, x, y);
        tl.SetupDefaultPins();
        return tl;
    }

    public override string FormatLabel(Device device) => string.Empty;

    public override DeviceParameterSpec? PrimaryParameter => new(
        "CharacteristicImpedance",
        "Impedance (Z0)",
        "Ω",
        min: 1.0,
        max: 1000.0,
        def: 50.0,
        step: 5.0,
        getter: d => d is TransmissionLineDevice tl ? tl.CharacteristicImpedance : 50.0,
        setter: (d, v) => { if (d is TransmissionLineDevice tl) tl.CharacteristicImpedance = Math.Max(0.1, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "DelaySeconds",
            "Delay Time (Td)",
            "s",
            min: 1e-12,
            max: 1e-6,
            def: 1e-9,
            step: 1e-10,
            getter: d => d is TransmissionLineDevice tl ? tl.DelaySeconds : 1e-9,
            setter: (d, v) => { if (d is TransmissionLineDevice tl) tl.DelaySeconds = Math.Max(1e-14, v); }
        )
    };
}
