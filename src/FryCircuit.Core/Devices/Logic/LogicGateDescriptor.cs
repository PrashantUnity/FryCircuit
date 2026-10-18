using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Logic;

public class LogicGateDescriptor : DeviceDescriptorBase
{
    private readonly string _gateType;

    public override string TypeName => _gateType;
    public override string DisplayName => $"{_gateType.ToUpperInvariant()} Gate";
    public override string Category => "Logic";

    public override IReadOnlyList<PinDefinition> Pins { get; }

    public LogicGateDescriptor(string gateType)
    {
        _gateType = gateType.ToLowerInvariant();
        if (_gateType == "not")
        {
            Pins = new List<PinDefinition>
            {
                new(0, "In", -1, 0, PinDirection.Left),
                new(1, "Out", 1, 0, PinDirection.Right)
            };
        }
        else
        {
            Pins = new List<PinDefinition>
            {
                new(0, "A", -1, -1, PinDirection.Left),
                new(1, "B", -1, 1, PinDirection.Left),
                new(2, "Out", 1, 0, PinDirection.Right)
            };
        }
    }

    public override Device CreateInstance(int id, double x, double y)
    {
        var gate = new LogicGateDevice(id, _gateType, x, y);
        gate.SetupDefaultPins();
        return gate;
    }

    public override string FormatLabel(Device device) => string.Empty;
}
