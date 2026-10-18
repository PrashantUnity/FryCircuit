using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Ground;

public class GroundDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "ground";
    public override string DisplayName => "Ground";
    public override string Category => "Sources";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "GND", 0, 0, PinDirection.Up)
    };

    public override (double Width, double Height) BoundingBoxSize => (1.6, 1.4);

    public override Device CreateInstance(int id, double x, double y)
    {
        var gnd = new GroundDevice(id, x, y);
        gnd.SetupDefaultPins();
        return gnd;
    }

    public override string FormatLabel(Device device) => "0 V";
}
