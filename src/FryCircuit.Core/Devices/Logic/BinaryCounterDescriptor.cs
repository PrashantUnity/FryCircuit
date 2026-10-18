using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Logic;

public class BinaryCounterDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "counter4";
    public override string DisplayName => "4-Bit Binary Counter";
    public override string Category => "Digital";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "UP", -1.5, -0.9, PinDirection.Left),
        new(1, "DOWN", -1.5, -0.3, PinDirection.Left),
        new(2, "CLR", -1.5, 0.9, PinDirection.Left),

        new(3, "1", 1.5, -0.9, PinDirection.Right),
        new(4, "2", 1.5, -0.3, PinDirection.Right),
        new(5, "4", 1.5, 0.3, PinDirection.Right),
        new(6, "8", 1.5, 0.9, PinDirection.Right),
        new(7, "CY", 1.5, 1.5, PinDirection.Right),
        new(8, "BW", -1.5, 1.5, PinDirection.Left)
    };

    public override (double Width, double Height) BoundingBoxSize => (3.2, 3.8);

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new BinaryCounterDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device) => "74193";

    public override DeviceParameterSpec? PrimaryParameter => new(
        "CounterValue",
        "Initial Value",
        "",
        min: 0,
        max: 15,
        def: 0,
        step: 1,
        getter: d => d is BinaryCounterDevice c ? c.CounterValue : 0,
        setter: (d, v) =>
        {
            if (d is BinaryCounterDevice c)
                c.CounterValue = (int)Math.Clamp(Math.Round(v), 0, 15);
        }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!
    };
}
