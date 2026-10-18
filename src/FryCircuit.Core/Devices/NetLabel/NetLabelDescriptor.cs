using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.NetLabel;

public class NetLabelDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "netlabel";
    public override string DisplayName => "Net Label";
    public override string Category => "Connections";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Net", 0, 0, PinDirection.Down)
    };

    public override (double Width, double Height) BoundingBoxSize => (1.5, 1.2);

    public override Device CreateInstance(int id, double x, double y)
    {
        var nl = new NetLabelDevice(id, x, y);
        nl.SetupDefaultPins();
        return nl;
    }

    public override string FormatLabel(Device device)
    {
        return device is NetLabelDevice nl ? nl.NetName : string.Empty;
    }
}
