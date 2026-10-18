using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.VoltageSource;

public class VoltageSourceDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "vsource";
    public override string DisplayName => "Voltage Source";
    public override string Category => "Sources";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "+", 0, -1, PinDirection.Up),
        new(1, "-", 0, 1, PinDirection.Down)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var vs = new VoltageSourceDevice(id, x, y);
        vs.SetupDefaultPins();
        return vs;
    }

    public override string FormatLabel(Device device)
    {
        if (device is not VoltageSourceDevice vs) return string.Empty;

        return vs.WaveformType == VoltageWaveformType.Sine
            ? $"{FormatEngineering(vs.Amplitude, "V")} @ {FormatEngineering(vs.Frequency, "Hz")}"
            : FormatEngineering(vs.Voltage, "V");
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Voltage",
        "Voltage / Amplitude",
        "V",
        min: -1000.0,
        max: 1000.0,
        def: 5.0,
        step: 0.5,
        getter: d => d is VoltageSourceDevice vs ? (vs.WaveformType == VoltageWaveformType.Sine ? vs.Amplitude : vs.Voltage) : 5.0,
        setter: (d, v) =>
        {
            if (d is VoltageSourceDevice vs)
            {
                if (vs.WaveformType == VoltageWaveformType.Sine)
                    vs.Amplitude = Math.Abs(v);
                else
                    vs.Voltage = v;
            }
        }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "Frequency",
            "Frequency",
            "Hz",
            min: 0.1,
            max: 1e9,
            def: 1000.0,
            step: 10.0,
            getter: d => d is VoltageSourceDevice vs ? vs.Frequency : 1000.0,
            setter: (d, v) => { if (d is VoltageSourceDevice vs) vs.Frequency = Math.Max(0.001, v); }
        ),
        new(
            "Offset",
            "DC Offset",
            "V",
            min: -100.0,
            max: 100.0,
            def: 0.0,
            step: 0.1,
            getter: d => d is VoltageSourceDevice vs ? vs.Offset : 0.0,
            setter: (d, v) => { if (d is VoltageSourceDevice vs) vs.Offset = v; }
        ),
        new(
            "Phase",
            "Phase Angle",
            "°",
            min: -360.0,
            max: 360.0,
            def: 0.0,
            step: 5.0,
            getter: d => d is VoltageSourceDevice vs ? vs.Phase : 0.0,
            setter: (d, v) => { if (d is VoltageSourceDevice vs) vs.Phase = v; }
        )
    };
}
