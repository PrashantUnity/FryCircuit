using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Switch;

public class SwitchDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "spst";
    public override string DisplayName => "SPST Switch";
    public override string Category => "Passives";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "A", 0, -1, PinDirection.Up),
        new(1, "B", 0, 1, PinDirection.Down)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var sw = new SwitchDevice(id, "spst", x, y);
        sw.SetupDefaultPins();
        return sw;
    }

    public override string FormatLabel(Device device)
    {
        return device is SwitchDevice sw ? (sw.IsClosed ? "CLOSED" : "OPEN") : string.Empty;
    }

    public override bool HandleClick(Device device)
    {
        if (device is SwitchDevice sw)
        {
            sw.Toggle();
            return true;
        }
        return false;
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "IsClosed",
        "Switch State (1=Closed, 0=Open)",
        "",
        min: 0.0,
        max: 1.0,
        def: 0.0,
        step: 1.0,
        getter: d => d is SwitchDevice sw ? (sw.IsClosed ? 1.0 : 0.0) : 0.0,
        setter: (d, v) => { if (d is SwitchDevice sw) sw.IsClosed = v >= 0.5; }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!
    };
}

public class SpdtSwitchDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "spdt";
    public override string DisplayName => "SPDT Switch";
    public override string Category => "Passives";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Common", -1, 0, PinDirection.Left),
        new(1, "Throw 1", 1, -1, PinDirection.Right),
        new(2, "Throw 2", 1, 1, PinDirection.Right)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var sw = new SwitchDevice(id, "spdt", x, y);
        sw.SetupDefaultPins();
        return sw;
    }

    public override string FormatLabel(Device device)
    {
        return device is SwitchDevice sw ? (sw.IsClosed ? "POS 1" : "POS 2") : string.Empty;
    }

    public override bool HandleClick(Device device)
    {
        if (device is SwitchDevice sw)
        {
            sw.Toggle();
            return true;
        }
        return false;
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "IsClosed",
        "Switch Position (1=Throw 1, 0=Throw 2)",
        "",
        min: 0.0,
        max: 1.0,
        def: 1.0,
        step: 1.0,
        getter: d => d is SwitchDevice sw ? (sw.IsClosed ? 1.0 : 0.0) : 0.0,
        setter: (d, v) => { if (d is SwitchDevice sw) sw.IsClosed = v >= 0.5; }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!
    };
}
