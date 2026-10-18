using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.CurrentSource;

public class CurrentSourceDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "csource";
    public override string DisplayName => "Current Source";
    public override string Category => "Sources";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "+", 0, -1, PinDirection.Up),
        new(1, "-", 0, 1, PinDirection.Down)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var cs = new CurrentSourceDevice(id, x, y);
        cs.SetupDefaultPins();
        return cs;
    }

    public override string FormatLabel(Device device)
    {
        return device is CurrentSourceDevice cs ? FormatEngineering(cs.CurrentValue, "A") : string.Empty;
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Current",
        "Current",
        "A",
        min: -100.0,
        max: 100.0,
        def: 0.001,
        step: 0.001,
        getter: d => d is CurrentSourceDevice cs ? cs.CurrentValue : 0.001,
        setter: (d, v) => { if (d is CurrentSourceDevice cs) cs.CurrentValue = v; }
    );
}
