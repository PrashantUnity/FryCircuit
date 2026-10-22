using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Aerospace;

public class BldcMotorDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "bldc";
    public override string DisplayName => "3-Phase BLDC Drone Motor";
    public override string Category => "Drones & Aero";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Phase U", -1, -1, PinDirection.Left),
        new(1, "Phase V", -1, 0, PinDirection.Left),
        new(2, "Phase W", -1, 1, PinDirection.Left)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.6, 2.6);

    public override Device CreateInstance(int id, double x, double y)
    {
        var motor = new BldcMotorDevice(id, x, y);
        motor.SetupDefaultPins();
        return motor;
    }

    public override string FormatLabel(Device device)
    {
        return device is BldcMotorDevice m
            ? $"{m.CurrentRpm:F0} RPM ({m.ThrustGrams:F0}g)"
            : string.Empty;
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Kv",
        "Velocity Constant (Kv)",
        "RPM/V",
        min: 100.0,
        max: 10000.0,
        def: 2300.0,
        step: 50.0,
        getter: d => d is BldcMotorDevice m ? m.Kv : 2300.0,
        setter: (d, v) => { if (d is BldcMotorDevice m) m.Kv = Math.Max(50.0, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "PhaseResistance",
            "Phase Resistance (Rs)",
            "Ω",
            min: 0.005,
            max: 5.0,
            def: 0.065,
            step: 0.005,
            getter: d => d is BldcMotorDevice m ? m.PhaseResistance : 0.065,
            setter: (d, v) => { if (d is BldcMotorDevice m) m.PhaseResistance = Math.Max(0.001, v); }
        ),
        new(
            "PropellerDiameterInches",
            "Prop Diameter",
            "in",
            min: 1.0,
            max: 30.0,
            def: 5.0,
            step: 0.5,
            getter: d => d is BldcMotorDevice m ? m.PropellerDiameterInches : 5.0,
            setter: (d, v) => { if (d is BldcMotorDevice m) m.PropellerDiameterInches = Math.Max(0.5, v); }
        ),
        new(
            "PolePairs",
            "Pole Pairs",
            "",
            min: 1.0,
            max: 24.0,
            def: 7.0,
            step: 1.0,
            getter: d => d is BldcMotorDevice m ? m.PolePairs : 7,
            setter: (d, v) => { if (d is BldcMotorDevice m) m.PolePairs = Math.Max(1, (int)Math.Round(v)); }
        )
    };
}
