using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Aerospace;

public class CosmicIonPulseDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "cosmicipulse";
    public override string DisplayName => "Cosmic Heavy-Ion Strike";
    public override string Category => "Aerospace & Space";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Strike (+)", 0, -1, PinDirection.Up),
        new(1, "Substrate (-)", 0, 1, PinDirection.Down)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var dev = new CosmicIonPulseDevice(id, x, y);
        dev.SetupDefaultPins();
        return dev;
    }

    public override string FormatLabel(Device device)
    {
        return device is CosmicIonPulseDevice ion
            ? FormatEngineering(ion.CollectedCharge, "C")
            : string.Empty;
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "CollectedCharge",
        "Charge (Q)",
        "C",
        min: 1e-15,
        max: 100e-12,
        def: 1e-12,
        step: 0.1e-12,
        getter: d => d is CosmicIonPulseDevice ion ? ion.CollectedCharge : 1e-12,
        setter: (d, v) => { if (d is CosmicIonPulseDevice ion) ion.CollectedCharge = Math.Max(1e-16, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "FallTimeSeconds",
            "Fall Time (tau2)",
            "s",
            min: 10e-12,
            max: 5e-9,
            def: 150e-12,
            step: 10e-12,
            getter: d => d is CosmicIonPulseDevice ion ? ion.FallTimeSeconds : 150e-12,
            setter: (d, v) => { if (d is CosmicIonPulseDevice ion) ion.FallTimeSeconds = Math.Max(1e-12, v); }
        ),
        new(
            "RiseTimeSeconds",
            "Rise Time (tau1)",
            "s",
            min: 1e-12,
            max: 500e-12,
            def: 10e-12,
            step: 1e-12,
            getter: d => d is CosmicIonPulseDevice ion ? ion.RiseTimeSeconds : 10e-12,
            setter: (d, v) => { if (d is CosmicIonPulseDevice ion) ion.RiseTimeSeconds = Math.Max(1e-15, v); }
        ),
        new(
            "StrikeTimeSeconds",
            "Strike Delay (t0)",
            "s",
            min: 0.0,
            max: 1.0,
            def: 1e-6,
            step: 1e-7,
            getter: d => d is CosmicIonPulseDevice ion ? ion.StrikeTimeSeconds : 1e-6,
            setter: (d, v) => { if (d is CosmicIonPulseDevice ion) ion.StrikeTimeSeconds = Math.Max(0.0, v); }
        ),
        new(
            "StrikePeriodSeconds",
            "Strike Period",
            "s",
            min: 0.0,
            max: 10.0,
            def: 0.0,
            step: 1e-6,
            getter: d => d is CosmicIonPulseDevice ion ? ion.StrikePeriodSeconds : 0.0,
            setter: (d, v) => { if (d is CosmicIonPulseDevice ion) ion.StrikePeriodSeconds = Math.Max(0.0, v); }
        )
    };
}
