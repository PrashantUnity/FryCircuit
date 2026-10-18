using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Battery;

public class BatteryCellDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "battery";
    public override string DisplayName => "LiPo Battery Cell";
    public override string Category => "Sources";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "+", -1, 0, PinDirection.Left),
        new(1, "-", 1, 0, PinDirection.Right)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var bat = new BatteryCellDevice(id, x, y);
        bat.SetupDefaultPins();
        return bat;
    }

    public override string FormatLabel(Device device)
    {
        return device is BatteryCellDevice b
            ? $"{b.OpenCircuitVoltage:F2}V ({(int)(b.StateOfCharge * 100)}%)"
            : string.Empty;
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "CapacityAmpHours",
        "Capacity (Ah)",
        "Ah",
        min: 0.1,
        max: 100.0,
        def: 2.2,
        step: 0.1,
        getter: d => d is BatteryCellDevice b ? b.CapacityAmpHours : 2.2,
        setter: (d, v) => { if (d is BatteryCellDevice b) b.CapacityAmpHours = Math.Max(0.01, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "StateOfCharge",
            "State of Charge (SOC)",
            "%",
            min: 0.0,
            max: 1.0,
            def: 0.90,
            step: 0.01,
            getter: d => d is BatteryCellDevice b ? b.StateOfCharge : 0.90,
            setter: (d, v) => { if (d is BatteryCellDevice b) b.StateOfCharge = Math.Clamp(v, 0.0, 1.0); }
        ),
        new(
            "NominalSeriesResistance",
            "Internal ESR (R0)",
            "Ω",
            min: 0.001,
            max: 10.0,
            def: 0.018,
            step: 0.001,
            getter: d => d is BatteryCellDevice b ? b.NominalSeriesResistance : 0.018,
            setter: (d, v) => { if (d is BatteryCellDevice b) b.NominalSeriesResistance = Math.Max(0.001, v); }
        )
    };
}
