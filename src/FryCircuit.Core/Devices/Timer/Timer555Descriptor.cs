using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Timer;

public class Timer555Descriptor : DeviceDescriptorBase
{
    public override string TypeName => "timer555";
    public override string DisplayName => "555 Timer IC";
    public override string Category => "Active";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "GND", 0, 1.5, PinDirection.Down),
        new(1, "TRIG", -1.5, 0.8, PinDirection.Left),
        new(2, "OUT", 1.5, 0.0, PinDirection.Right),
        new(3, "RESET", -1.5, -1.0, PinDirection.Left),
        new(4, "CTRL", 1.5, 1.0, PinDirection.Right),
        new(5, "THRESH", -1.5, 0.2, PinDirection.Left),
        new(6, "DISCH", -1.5, -0.4, PinDirection.Left),
        new(7, "VCC", 0, -1.5, PinDirection.Up)
    };

    public override (double Width, double Height) BoundingBoxSize => (3.2, 3.2);

    public override Device CreateInstance(int id, double x, double y)
    {
        var t = new Timer555Device(id, x, y);
        t.SetupDefaultPins();
        return t;
    }

    public override string FormatLabel(Device device) => string.Empty;

    public override DeviceParameterSpec? PrimaryParameter => new(
        "OutputResistance",
        "Output Stage R",
        "Ω",
        min: 1.0,
        max: 500.0,
        def: 20.0,
        step: 1.0,
        getter: d => d is Timer555Device t ? t.OutputResistance : 20.0,
        setter: (d, v) => { if (d is Timer555Device t) t.OutputResistance = Math.Max(0.1, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "DischargeOnResistance",
            "Discharge Transistor Ron",
            "Ω",
            min: 0.1,
            max: 200.0,
            def: 10.0,
            step: 0.5,
            getter: d => d is Timer555Device t ? t.DischargeOnResistance : 10.0,
            setter: (d, v) => { if (d is Timer555Device t) t.DischargeOnResistance = Math.Max(0.01, v); }
        ),
        new(
            "DividerResistor",
            "Internal Ladder R (3x)",
            "Ω",
            min: 500.0,
            max: 1e6,
            def: 5000.0,
            step: 100.0,
            getter: d => d is Timer555Device t ? t.DividerResistor : 5000.0,
            setter: (d, v) => { if (d is Timer555Device t) t.DividerResistor = Math.Max(10.0, v); }
        )
    };
}
