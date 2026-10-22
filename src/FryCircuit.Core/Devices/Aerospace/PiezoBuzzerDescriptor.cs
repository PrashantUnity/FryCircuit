using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Aerospace;

public class PiezoBuzzerDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "buzzer";
    public override string DisplayName => "Drone Piezo Buzzer";
    public override string Category => "Drones & Aero";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "+", 0, -1, PinDirection.Up),
        new(1, "-", 0, 1, PinDirection.Down)
    };

    public override (double Width, double Height) BoundingBoxSize => (2.2, 2.2);

    public override Device CreateInstance(int id, double x, double y)
    {
        var buzz = new PiezoBuzzerDevice(id, x, y);
        buzz.SetupDefaultPins();
        return buzz;
    }

    public override string FormatLabel(Device device)
    {
        return device is PiezoBuzzerDevice b
            ? (b.IsSounding ? $"{b.ActualDecibelsSpl:F0} dB ({b.ResonantFrequencyHz:F0}Hz)" : "OFF")
            : string.Empty;
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "NominalVoltage",
        "Nominal Voltage",
        "V",
        min: 1.5,
        max: 24.0,
        def: 5.0,
        step: 0.5,
        getter: d => d is PiezoBuzzerDevice b ? b.NominalVoltage : 5.0,
        setter: (d, v) => { if (d is PiezoBuzzerDevice b) b.NominalVoltage = Math.Max(1.0, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "ResonantFrequencyHz",
            "Frequency",
            "Hz",
            min: 500.0,
            max: 10000.0,
            def: 2700.0,
            step: 100.0,
            getter: d => d is PiezoBuzzerDevice b ? b.ResonantFrequencyHz : 2700.0,
            setter: (d, v) => { if (d is PiezoBuzzerDevice b) b.ResonantFrequencyHz = Math.Max(100.0, v); }
        ),
        new(
            "RatedDecibelsSpl",
            "Sound Level",
            "dBA",
            min: 60.0,
            max: 120.0,
            def: 85.0,
            step: 1.0,
            getter: d => d is PiezoBuzzerDevice b ? b.RatedDecibelsSpl : 85.0,
            setter: (d, v) => { if (d is PiezoBuzzerDevice b) b.RatedDecibelsSpl = Math.Clamp(v, 40.0, 130.0); }
        ),
        new(
            "MinOperateVoltage",
            "Threshold Voltage",
            "V",
            min: 0.5,
            max: 12.0,
            def: 2.5,
            step: 0.5,
            getter: d => d is PiezoBuzzerDevice b ? b.MinOperateVoltage : 2.5,
            setter: (d, v) => { if (d is PiezoBuzzerDevice b) b.MinOperateVoltage = Math.Max(0.1, v); }
        )
    };
}
