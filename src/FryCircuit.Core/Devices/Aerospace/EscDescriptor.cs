using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Aerospace;

public class EscDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "esc";
    public override string DisplayName => "Electronic Speed Controller (ESC)";
    public override string Category => "Drones & Aero";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "VBAT (+)", -2, -1, PinDirection.Left),
        new(1, "GND (-)", -2, 1, PinDirection.Left),
        new(2, "PWM In", -2, 0, PinDirection.Left),
        new(3, "Phase U", 2, -1, PinDirection.Right),
        new(4, "Phase V", 2, 0, PinDirection.Right),
        new(5, "Phase W", 2, 1, PinDirection.Right)
    };

    public override (double Width, double Height) BoundingBoxSize => (4.4, 2.6);

    public override Device CreateInstance(int id, double x, double y)
    {
        var esc = new EscDevice(id, x, y);
        esc.SetupDefaultPins();
        return esc;
    }

    public override string FormatLabel(Device device)
    {
        return device is EscDevice esc
            ? $"{esc.ContinuousCurrentRating:F0}A ({(int)(esc.EffectiveThrottle * 100)}%)"
            : string.Empty;
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "ContinuousCurrentRating",
        "Current Rating",
        "A",
        min: 5.0,
        max: 120.0,
        def: 45.0,
        step: 5.0,
        getter: d => d is EscDevice e ? e.ContinuousCurrentRating : 45.0,
        setter: (d, v) => { if (d is EscDevice e) e.ContinuousCurrentRating = Math.Max(1.0, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "ThrottlePercent",
            "Manual Throttle",
            "%",
            min: 0.0,
            max: 1.0,
            def: 0.75,
            step: 0.05,
            getter: d => d is EscDevice e ? e.ThrottlePercent : 0.75,
            setter: (d, v) => { if (d is EscDevice e) e.ThrottlePercent = Math.Clamp(v, 0.0, 1.0); }
        ),
        new(
            "MosfetRdsOn",
            "MOSFET Rds(on)",
            "Ω",
            min: 0.0005,
            max: 0.05,
            def: 0.0025,
            step: 0.0005,
            getter: d => d is EscDevice e ? e.MosfetRdsOn : 0.0025,
            setter: (d, v) => { if (d is EscDevice e) e.MosfetRdsOn = Math.Max(0.0001, v); }
        )
    };
}
