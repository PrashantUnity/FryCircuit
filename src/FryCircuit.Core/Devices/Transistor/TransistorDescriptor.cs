using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Transistor;

public class BjtDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "npn";
    public override string DisplayName => "NPN BJT";
    public override string Category => "Semiconductors";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Base", -1, 0, PinDirection.Left),
        new(1, "Collector", 1, -1, PinDirection.Up),
        new(2, "Emitter", 1, 1, PinDirection.Down)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var bjt = new BjtDevice(id, "npn", x, y);
        bjt.SetupDefaultPins();
        return bjt;
    }

    public override string FormatLabel(Device device) => device.Type.ToUpperInvariant();

    public override DeviceParameterSpec? PrimaryParameter => new(
        "BetaF",
        "Forward Beta (hFE)",
        "",
        min: 10.0,
        max: 1000.0,
        def: 100.0,
        step: 5.0,
        getter: d => d is BjtDevice b ? b.BetaF : 100.0,
        setter: (d, v) => { if (d is BjtDevice b) b.BetaF = Math.Max(1.0, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "Isat",
            "Saturation Current (Is)",
            "A",
            min: 1e-16,
            max: 1e-11,
            def: 1e-14,
            step: 1e-15,
            getter: d => d is BjtDevice b ? b.Isat : 1e-14,
            setter: (d, v) => { if (d is BjtDevice b) b.Isat = Math.Max(1e-18, v); }
        ),
        new(
            "EarlyVoltage",
            "Early Voltage (VAF)",
            "V",
            min: 10.0,
            max: 500.0,
            def: 100.0,
            step: 5.0,
            getter: d => d is BjtDevice b ? b.GetParameter("vaf", 100.0) : 100.0,
            setter: (d, v) => { if (d is BjtDevice b) b.SetParameter("vaf", Math.Max(1.0, v)); }
        )
    };
}

public class PnpBjtDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "pnp";
    public override string DisplayName => "PNP BJT";
    public override string Category => "Semiconductors";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Base", -1, 0, PinDirection.Left),
        new(1, "Collector", 1, -1, PinDirection.Up),
        new(2, "Emitter", 1, 1, PinDirection.Down)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var bjt = new BjtDevice(id, "pnp", x, y);
        bjt.SetupDefaultPins();
        return bjt;
    }

    public override string FormatLabel(Device device) => "PNP";

    public override DeviceParameterSpec? PrimaryParameter => new(
        "BetaF",
        "Forward Beta (hFE)",
        "",
        min: 10.0,
        max: 1000.0,
        def: 100.0,
        step: 5.0,
        getter: d => d is BjtDevice b ? b.BetaF : 100.0,
        setter: (d, v) => { if (d is BjtDevice b) b.BetaF = Math.Max(1.0, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "Isat",
            "Saturation Current (Is)",
            "A",
            min: 1e-16,
            max: 1e-11,
            def: 1e-14,
            step: 1e-15,
            getter: d => d is BjtDevice b ? b.Isat : 1e-14,
            setter: (d, v) => { if (d is BjtDevice b) b.Isat = Math.Max(1e-18, v); }
        )
    };
}

public class MosfetDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "nmos";
    public override string DisplayName => "NMOS FET";
    public override string Category => "Semiconductors";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Gate", -1, 0, PinDirection.Left),
        new(1, "Drain", 1, -1, PinDirection.Up),
        new(2, "Source", 1, 1, PinDirection.Down)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var mos = new MosfetDevice(id, "nmos", x, y);
        mos.SetupDefaultPins();
        return mos;
    }

    public override string FormatLabel(Device device) => device.Type.ToUpperInvariant();

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Vth",
        "Threshold Voltage (Vth)",
        "V",
        min: 0.1,
        max: 10.0,
        def: 1.5,
        step: 0.1,
        getter: d => d is MosfetDevice m ? m.Vth : 1.5,
        setter: (d, v) => { if (d is MosfetDevice m) m.Vth = v; }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "Kp",
            "Transconductance (Kp)",
            "A/V²",
            min: 0.0001,
            max: 0.5,
            def: 0.005,
            step: 0.001,
            getter: d => d is MosfetDevice m ? m.Kp : 0.005,
            setter: (d, v) => { if (d is MosfetDevice m) m.Kp = Math.Max(1e-5, v); }
        ),
        new(
            "Lambda",
            "Channel Modulation (λ)",
            "1/V",
            min: 0.0,
            max: 0.2,
            def: 0.02,
            step: 0.005,
            getter: d => d is MosfetDevice m ? m.Lambda : 0.02,
            setter: (d, v) => { if (d is MosfetDevice m) m.Lambda = Math.Max(0.0, v); }
        ),
        new(
            "Cgs",
            "Gate-Source Cap (Cgs)",
            "F",
            min: 1e-13,
            max: 500e-12,
            def: 20e-12,
            step: 5e-12,
            getter: d => d is MosfetDevice m ? m.Cgs : 20e-12,
            setter: (d, v) => { if (d is MosfetDevice m) m.Cgs = Math.Max(1e-15, v); }
        ),
        new(
            "Cgd",
            "Miller Cap (Cgd)",
            "F",
            min: 1e-13,
            max: 100e-12,
            def: 5e-12,
            step: 1e-12,
            getter: d => d is MosfetDevice m ? m.Cgd : 5e-12,
            setter: (d, v) => { if (d is MosfetDevice m) m.Cgd = Math.Max(1e-15, v); }
        )
    };
}

public class PmosDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "pmos";
    public override string DisplayName => "PMOS FET";
    public override string Category => "Semiconductors";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Gate", -1, 0, PinDirection.Left),
        new(1, "Drain", 1, -1, PinDirection.Up),
        new(2, "Source", 1, 1, PinDirection.Down)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var mos = new MosfetDevice(id, "pmos", x, y);
        mos.SetupDefaultPins();
        return mos;
    }

    public override string FormatLabel(Device device) => "PMOS";

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Vth",
        "Threshold Voltage (Vth)",
        "V",
        min: -10.0,
        max: -0.1,
        def: -1.5,
        step: 0.1,
        getter: d => d is MosfetDevice m ? m.Vth : -1.5,
        setter: (d, v) => { if (d is MosfetDevice m) m.Vth = v; }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "Kp",
            "Transconductance (Kp)",
            "A/V²",
            min: 0.0001,
            max: 0.5,
            def: 0.005,
            step: 0.001,
            getter: d => d is MosfetDevice m ? m.Kp : 0.005,
            setter: (d, v) => { if (d is MosfetDevice m) m.Kp = Math.Max(1e-5, v); }
        ),
        new(
            "Lambda",
            "Channel Modulation (λ)",
            "1/V",
            min: 0.0,
            max: 0.2,
            def: 0.02,
            step: 0.005,
            getter: d => d is MosfetDevice m ? m.Lambda : 0.02,
            setter: (d, v) => { if (d is MosfetDevice m) m.Lambda = Math.Max(0.0, v); }
        )
    };
}
