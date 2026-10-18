using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Diode;

public class DiodeDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "diode";
    public override string DisplayName => "Diode";
    public override string Category => "Semiconductors";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Anode", 0, -1, PinDirection.Up),
        new(1, "Cathode", 0, 1, PinDirection.Down)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var d = new DiodeDevice(id, x, y);
        d.SetupDefaultPins();
        return d;
    }

    public override string FormatLabel(Device device)
    {
        if (device is LedDevice led)
            return "LED";
        if (device is ZenerDiodeDevice zener)
            return $"{zener.BreakdownVoltage:F1} V Zener";
        return "1N4148";
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "SaturationCurrent",
        "Saturation Current (Is)",
        "A",
        min: 1e-18,
        max: 1e-6,
        def: 1e-14,
        step: 1e-15,
        getter: d => d is DiodeDevice dd ? dd.SaturationCurrent : 1e-14,
        setter: (d, v) => { if (d is DiodeDevice dd) dd.SaturationCurrent = Math.Max(1e-20, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "EmissionCoefficient",
            "Emission Coeff (n)",
            "",
            min: 0.5,
            max: 3.0,
            def: 1.0,
            step: 0.05,
            getter: d => d is DiodeDevice dd ? dd.EmissionCoefficient : 1.0,
            setter: (d, v) => { if (d is DiodeDevice dd) dd.EmissionCoefficient = Math.Clamp(v, 0.5, 3.0); }
        ),
        new(
            "ZeroBiasCapacitance",
            "Junction Cap (Cj0)",
            "F",
            min: 1e-15,
            max: 1e-9,
            def: 2e-12,
            step: 1e-13,
            getter: d => d is DiodeDevice dd ? dd.ZeroBiasCapacitance : 2e-12,
            setter: (d, v) => { if (d is DiodeDevice dd) dd.ZeroBiasCapacitance = Math.Max(1e-16, v); }
        ),
        new(
            "TransitTime",
            "Transit Time (tau)",
            "s",
            min: 1e-12,
            max: 1e-6,
            def: 5e-10,
            step: 1e-11,
            getter: d => d is DiodeDevice dd ? dd.TransitTime : 5e-10,
            setter: (d, v) => { if (d is DiodeDevice dd) dd.TransitTime = Math.Max(1e-14, v); }
        )
    };
}

public class ZenerDiodeDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "zener";
    public override string DisplayName => "Zener Diode";
    public override string Category => "Semiconductors";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Anode", 0, -1, PinDirection.Up),
        new(1, "Cathode", 0, 1, PinDirection.Down)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var z = new ZenerDiodeDevice(id, x, y);
        z.SetupDefaultPins();
        return z;
    }

    public override string FormatLabel(Device device)
    {
        return device is ZenerDiodeDevice zd ? $"{zd.BreakdownVoltage:F1} V" : "5.1 V";
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "BreakdownVoltage",
        "Zener Breakdown (Vz)",
        "V",
        min: 1.0,
        max: 100.0,
        def: 5.1,
        step: 0.1,
        getter: d => d is ZenerDiodeDevice zd ? zd.BreakdownVoltage : 5.1,
        setter: (d, v) => { if (d is ZenerDiodeDevice zd) zd.BreakdownVoltage = Math.Max(0.5, v); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!,
        new(
            "ZenerResistance",
            "Zener Dyn Resistance (Rz)",
            "Ω",
            min: 0.1,
            max: 500.0,
            def: 10.0,
            step: 0.5,
            getter: d => d is ZenerDiodeDevice zd ? zd.ZenerResistance : 10.0,
            setter: (d, v) => { if (d is ZenerDiodeDevice zd) zd.ZenerResistance = Math.Max(0.01, v); }
        )
    };
}

public class LedDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "led";
    public override string DisplayName => "LED";
    public override string Category => "Semiconductors";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Anode", 0, -1, PinDirection.Up),
        new(1, "Cathode", 0, 1, PinDirection.Down)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var led = new LedDevice(id, x, y);
        led.SetupDefaultPins();
        return led;
    }

    public override string FormatLabel(Device device)
    {
        if (device is LedDevice led)
        {
            return led.Wavelength <= 0.0 ? "White" : led.ColorName;
        }
        return "LED";
    }

    public override DeviceParameterSpec? PrimaryParameter => new(
        "Wavelength",
        "Wavelength",
        "nm",
        min: 380.0,
        max: 780.0,
        def: 650.0,
        step: 5.0,
        getter: d => d is LedDevice led ? led.Wavelength : 650.0,
        setter: (d, v) => { if (d is LedDevice led) led.Wavelength = Math.Clamp(v, 0.0, 780.0); }
    );

    public override IReadOnlyList<DeviceParameterSpec> Parameters => new List<DeviceParameterSpec>
    {
        PrimaryParameter!
    };
}
