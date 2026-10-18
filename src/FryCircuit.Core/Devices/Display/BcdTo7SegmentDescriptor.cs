using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Display;

public class BcdTo7SegmentDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "bcd7seg";
    public override string DisplayName => "BCD to 7-Segment Decoder";
    public override string Category => "Digital";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "A", -1.5, -0.6, PinDirection.Left),
        new(1, "B", -1.5, -0.2, PinDirection.Left),
        new(2, "C", -1.5, 0.2, PinDirection.Left),
        new(3, "D", -1.5, 0.6, PinDirection.Left),

        new(4, "a", 1.5, -1.2, PinDirection.Right),
        new(5, "b", 1.5, -0.8, PinDirection.Right),
        new(6, "c", 1.5, -0.4, PinDirection.Right),
        new(7, "d", 1.5, 0.0, PinDirection.Right),
        new(8, "e", 1.5, 0.4, PinDirection.Right),
        new(9, "f", 1.5, 0.8, PinDirection.Right),
        new(10, "g", 1.5, 1.2, PinDirection.Right),
    };

    public override (double Width, double Height) BoundingBoxSize => (3.2, 3.2);

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new BcdTo7SegmentDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device) => string.Empty;
}
