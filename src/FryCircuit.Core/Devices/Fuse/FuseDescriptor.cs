using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Fuse;

public class FuseDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "fuse";
    public override string DisplayName => "Fuse";
    public override string Category => "Passives";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "A", 0, -1, PinDirection.Up),
        new(1, "B", 0, 1, PinDirection.Down)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var fuse = new FuseDevice(id, x, y);
        fuse.SetupDefaultPins();
        return fuse;
    }

    public override string FormatLabel(Device device)
    {
        return device is FuseDevice fuse
            ? $"{fuse.CurrentRating:G3}A Fuse"
            : "1A Fuse";
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "CurrentRating",
        "Current Rating",
        "A",
        min: 0.05,
        max: 50.0,
        def: 1.0,
        step: 0.25,
        getter: d => d is FuseDevice f ? f.CurrentRating : 1.0,
        setter: (d, v) => { if (d is FuseDevice f) f.CurrentRating = Math.Max(0.01, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "InternalResistance",
            "Cold Resistance",
            "Ω",
            min: 0.001,
            max: 5.0,
            def: 0.05,
            step: 0.01,
            getter: d => d is FuseDevice f ? f.InternalResistance : 0.05,
            setter: (d, v) => { if (d is FuseDevice f) f.InternalResistance = Math.Max(1e-4, v); }
        )
    };
}
