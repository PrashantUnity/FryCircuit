using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Display;

public class SevenSegmentDisplayDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "seg7";
    public override string DisplayName => "7-Segment Display";
    public override string Category => "Display";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "a", -1.5, -1.2, PinDirection.Left),
        new(1, "b", -1.5, -0.8, PinDirection.Left),
        new(2, "c", -1.5, -0.4, PinDirection.Left),
        new(3, "d", -1.5, 0.0, PinDirection.Left),
        new(4, "e", -1.5, 0.4, PinDirection.Left),
        new(5, "f", -1.5, 0.8, PinDirection.Left),
        new(6, "g", -1.5, 1.2, PinDirection.Left),
        new(7, "COM", 0.0, 1.8, PinDirection.Down),
        new(8, "DP", 1.5, 1.2, PinDirection.Right)
    };

    public override (double Width, double Height) BoundingBoxSize => (3.2, 3.8);

    public override Device CreateInstance(int id, double x, double y)
    {
        var seg = new SevenSegmentDisplayDevice(id, x, y);
        seg.SetupDefaultPins();
        return seg;
    }

    public override string FormatLabel(Device device) => string.Empty;

    public override DeviceParameterSpec? PrimaryParameter => new(
        "ManualHexValue",
        "Override Hex Digit",
        "",
        min: -1,
        max: 15,
        def: -1,
        step: 1,
        getter: d => d is SevenSegmentDisplayDevice s ? (s.ManualHexValue ?? -1) : -1,
        setter: (d, v) =>
        {
            if (d is SevenSegmentDisplayDevice s)
            {
                int intV = (int)Math.Round(v);
                s.ManualHexValue = (intV >= 0 && intV <= 15) ? intV : null;
            }
        }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!
    };
}
