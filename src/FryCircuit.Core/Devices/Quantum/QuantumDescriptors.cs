using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Quantum;

public class JosephsonJunctionDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "josephson";
    public override string DisplayName => "Josephson Junction";
    public override string Category => "Quantum";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "+", -1, 0, PinDirection.Left),
        new(1, "-", 1, 0, PinDirection.Right)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var jj = new JosephsonJunctionDevice(id, x, y);
        jj.SetupDefaultPins();
        return jj;
    }

    public override string FormatLabel(Device device)
    {
        return device is JosephsonJunctionDevice jj ? $"{FormatEngineering(jj.CriticalCurrent, "A")} Ic" : string.Empty;
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "CriticalCurrent",
        "Critical Current (Ic)",
        "A",
        min: 1e-9,
        max: 1e-3,
        def: 10e-6,
        step: 1e-6,
        getter: d => d is JosephsonJunctionDevice jj ? jj.CriticalCurrent : 10e-6,
        setter: (d, v) => { if (d is JosephsonJunctionDevice jj) jj.CriticalCurrent = Math.Max(1e-12, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "NormalResistance",
            "Normal Resistance (Rn)",
            "Ω",
            min: 1.0,
            max: 10000.0,
            def: 250.0,
            step: 10.0,
            getter: d => d is JosephsonJunctionDevice jj ? jj.NormalResistance : 250.0,
            setter: (d, v) => { if (d is JosephsonJunctionDevice jj) jj.NormalResistance = Math.Max(0.1, v); }
        ),
        new(
            "JunctionCapacitance",
            "Junction Capacitance (Cj)",
            "F",
            min: 1e-16,
            max: 1e-9,
            def: 50e-15,
            step: 5e-15,
            getter: d => d is JosephsonJunctionDevice jj ? jj.JunctionCapacitance : 50e-15,
            setter: (d, v) => { if (d is JosephsonJunctionDevice jj) jj.JunctionCapacitance = Math.Max(1e-17, v); }
        )
    };
}

public class SquidDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "squid";
    public override string DisplayName => "DC SQUID";
    public override string Category => "Quantum";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "+", 0, -1, PinDirection.Up),
        new(1, "-", 0, 1, PinDirection.Down)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var sq = new SquidDevice(id, x, y);
        sq.SetupDefaultPins();
        return sq;
    }

    public override string FormatLabel(Device device)
    {
        return device is SquidDevice sq ? $"{FormatEngineering(sq.ModulatedCriticalCurrent, "A")} ({sq.ExternalFluxNormalized:F2}Φ₀)" : string.Empty;
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "BaseCriticalCurrent",
        "Base Critical Current (Ic0)",
        "A",
        min: 1e-9,
        max: 1e-3,
        def: 20e-6,
        step: 1e-6,
        getter: d => d is SquidDevice sq ? sq.BaseCriticalCurrent : 20e-6,
        setter: (d, v) => { if (d is SquidDevice sq) sq.BaseCriticalCurrent = Math.Max(1e-12, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "ExternalFluxNormalized",
            "Magnetic Flux Bias (Φ/Φ₀)",
            "Φ₀",
            min: -5.0,
            max: 5.0,
            def: 0.0,
            step: 0.05,
            getter: d => d is SquidDevice sq ? sq.ExternalFluxNormalized : 0.0,
            setter: (d, v) => { if (d is SquidDevice sq) sq.ExternalFluxNormalized = v; }
        ),
        new(
            "NormalResistance",
            "Normal Resistance (Rn)",
            "Ω",
            min: 1.0,
            max: 10000.0,
            def: 150.0,
            step: 10.0,
            getter: d => d is SquidDevice sq ? sq.NormalResistance : 150.0,
            setter: (d, v) => { if (d is SquidDevice sq) sq.NormalResistance = Math.Max(0.1, v); }
        ),
        new(
            "TotalCapacitance",
            "Loop Capacitance (Cj)",
            "F",
            min: 1e-16,
            max: 1e-9,
            def: 100e-15,
            step: 10e-15,
            getter: d => d is SquidDevice sq ? sq.TotalCapacitance : 100e-15,
            setter: (d, v) => { if (d is SquidDevice sq) sq.TotalCapacitance = Math.Max(1e-17, v); }
        )
    };
}

public class TunnelDiodeDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "tunneldiode";
    public override string DisplayName => "Tunnel Diode";
    public override string Category => "Quantum";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Anode", -1, 0, PinDirection.Left),
        new(1, "Cathode", 1, 0, PinDirection.Right)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var td = new TunnelDiodeDevice(id, x, y);
        td.SetupDefaultPins();
        return td;
    }

    public override string FormatLabel(Device device)
    {
        return device is TunnelDiodeDevice td ? $"{FormatEngineering(td.PeakCurrent, "A")} Ip" : string.Empty;
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "PeakCurrent",
        "Peak Current (Ip)",
        "A",
        min: 1e-6,
        max: 100e-3,
        def: 5e-3,
        step: 0.5e-3,
        getter: d => d is TunnelDiodeDevice td ? td.PeakCurrent : 5e-3,
        setter: (d, v) => { if (d is TunnelDiodeDevice td) td.PeakCurrent = Math.Max(1e-9, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "ValleyCurrent",
            "Valley Current (Iv)",
            "A",
            min: 1e-7,
            max: 50e-3,
            def: 0.6e-3,
            step: 0.1e-3,
            getter: d => d is TunnelDiodeDevice td ? td.ValleyCurrent : 0.6e-3,
            setter: (d, v) => { if (d is TunnelDiodeDevice td) td.ValleyCurrent = Math.Max(1e-9, v); }
        ),
        new(
            "PeakVoltage",
            "Peak Voltage (Vp)",
            "V",
            min: 0.01,
            max: 0.5,
            def: 0.065,
            step: 0.005,
            getter: d => d is TunnelDiodeDevice td ? td.PeakVoltage : 0.065,
            setter: (d, v) => { if (d is TunnelDiodeDevice td) td.PeakVoltage = Math.Max(0.001, v); }
        ),
        new(
            "ValleyVoltage",
            "Valley Voltage (Vv)",
            "V",
            min: 0.05,
            max: 1.5,
            def: 0.35,
            step: 0.01,
            getter: d => d is TunnelDiodeDevice td ? td.ValleyVoltage : 0.35,
            setter: (d, v) => { if (d is TunnelDiodeDevice td) td.ValleyVoltage = Math.Max(0.01, v); }
        ),
        new(
            "JunctionCapacitance",
            "Junction Capacitance (Cj)",
            "F",
            min: 1e-15,
            max: 1e-9,
            def: 5e-12,
            step: 0.5e-12,
            getter: d => d is TunnelDiodeDevice td ? td.JunctionCapacitance : 5e-12,
            setter: (d, v) => { if (d is TunnelDiodeDevice td) td.JunctionCapacitance = Math.Max(1e-16, v); }
        )
    };
}
