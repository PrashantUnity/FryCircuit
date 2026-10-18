using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Inductor;

public class InductorDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "inductor";
    public override string DisplayName => "Inductor";
    public override string Category => "Passives";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "A", 0, -1, PinDirection.Up),
        new(1, "B", 0, 1, PinDirection.Down)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var ind = new InductorDevice(id, x, y);
        ind.SetupDefaultPins();
        return ind;
    }

    public override string FormatLabel(Device device)
    {
        return device is InductorDevice ind ? FormatEngineering(ind.Inductance, "H") : string.Empty;
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Inductance",
        "Inductance",
        "H",
        min: 1e-12,
        max: 100.0,
        def: 1e-3,
        step: 1e-4,
        getter: d => d is InductorDevice i ? i.Inductance : 1e-3,
        setter: (d, v) => { if (d is InductorDevice i) i.Inductance = Math.Max(1e-13, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "SeriesResistance",
            "Winding ESR (Rs)",
            "Ω",
            min: 0.001,
            max: 50.0,
            def: 0.05,
            step: 0.01,
            getter: d => d is InductorDevice ind ? ind.SeriesResistance : 0.05,
            setter: (d, v) => { if (d is InductorDevice ind) ind.SeriesResistance = Math.Max(1e-6, v); }
        ),
        new(
            "StateCurrent",
            "Initial / Current (I0)",
            "A",
            min: -100.0,
            max: 100.0,
            def: 0.0,
            step: 0.01,
            getter: d => d is InductorDevice ind ? ind.StateCurrent : 0.0,
            setter: (d, v) => { if (d is InductorDevice ind) { ind.StateCurrent = v; ind.Current = v; } }
        )
    };
}
