using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Sensors;

public class HallEffectSensorDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "hallsensor";
    public override string DisplayName => "Hall Effect Sensor";
    public override string Category => "Sensors";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Vcc", -1, -1, PinDirection.Left),
        new(1, "GND", -1, 1, PinDirection.Left),
        new(2, "Out (+)", 1, -1, PinDirection.Right),
        new(3, "Out (-)", 1, 1, PinDirection.Right)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.2, 2.2);

    public override Device CreateInstance(int id, double x, double y)
    {
        var hs = new HallEffectSensorDevice(id, x, y);
        hs.SetupDefaultPins();
        return hs;
    }

    public override string FormatLabel(Device device)
    {
        return device is HallEffectSensorDevice hs ? $"{hs.MagneticFieldTesla * 1000:F0} mT" : string.Empty;
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "MagneticFieldTesla",
        "Magnetic Field (B)",
        "T",
        min: -1.0,
        max: 1.0,
        def: 0.05,
        step: 0.005,
        getter: d => d is HallEffectSensorDevice hs ? hs.MagneticFieldTesla : 0.05,
        setter: (d, v) => { if (d is HallEffectSensorDevice hs) hs.MagneticFieldTesla = v; }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "SensitivityVoltsPerTesla",
            "Sensitivity (Sh)",
            "V/T",
            min: 0.1,
            max: 500.0,
            def: 25.0,
            step: 1.0,
            getter: d => d is HallEffectSensorDevice hs ? hs.SensitivityVoltsPerTesla : 25.0,
            setter: (d, v) => { if (d is HallEffectSensorDevice hs) hs.SensitivityVoltsPerTesla = Math.Max(0.01, v); }
        ),
        new(
            "InputResistance",
            "Input Resistance (Rin)",
            "Ω",
            min: 10.0,
            max: 100000.0,
            def: 1000.0,
            step: 50.0,
            getter: d => d is HallEffectSensorDevice hs ? hs.InputResistance : 1000.0,
            setter: (d, v) => { if (d is HallEffectSensorDevice hs) hs.InputResistance = Math.Max(1.0, v); }
        ),
        new(
            "OutputResistance",
            "Output Resistance (Rout)",
            "Ω",
            min: 1.0,
            max: 10000.0,
            def: 100.0,
            step: 10.0,
            getter: d => d is HallEffectSensorDevice hs ? hs.OutputResistance : 100.0,
            setter: (d, v) => { if (d is HallEffectSensorDevice hs) hs.OutputResistance = Math.Max(0.1, v); }
        )
    };
}
