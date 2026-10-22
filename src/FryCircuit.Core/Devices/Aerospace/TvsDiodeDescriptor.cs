using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Aerospace;

public class TvsDiodeDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "tvs";
    public override string DisplayName => "TVS Clamping Diode";
    public override string Category => "Drones & Aero";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Anode", -1, 0, PinDirection.Left),
        new(1, "Cathode", 1, 0, PinDirection.Right)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.2, 2.0);

    public override Device CreateInstance(int id, double x, double y)
    {
        var tvs = new TvsDiodeDevice(id, x, y);
        tvs.SetupDefaultPins();
        return tvs;
    }

    public override string FormatLabel(Device device)
    {
        return device is TvsDiodeDevice tvs
            ? $"{tvs.BreakdownVoltage:F1}V TVS"
            : string.Empty;
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "BreakdownVoltage",
        "Breakdown (Vbr)",
        "V",
        min: 3.3,
        max: 100.0,
        def: 18.0,
        step: 1.0,
        getter: d => d is TvsDiodeDevice t ? t.BreakdownVoltage : 18.0,
        setter: (d, v) => { if (d is TvsDiodeDevice t) t.BreakdownVoltage = Math.Max(1.0, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "ClampingResistance",
            "Clamping ESR",
            "Ω",
            min: 0.005,
            max: 5.0,
            def: 0.08,
            step: 0.01,
            getter: d => d is TvsDiodeDevice t ? t.ClampingResistance : 0.08,
            setter: (d, v) => { if (d is TvsDiodeDevice t) t.ClampingResistance = Math.Max(0.001, v); }
        ),
        new(
            "PeakPowerWatts",
            "Peak Power",
            "W",
            min: 100.0,
            max: 5000.0,
            def: 1500.0,
            step: 100.0,
            getter: d => d is TvsDiodeDevice t ? t.PeakPowerWatts : 1500.0,
            setter: (d, v) => { if (d is TvsDiodeDevice t) t.PeakPowerWatts = Math.Max(10.0, v); }
        )
    };
}
