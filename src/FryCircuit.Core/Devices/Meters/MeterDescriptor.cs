using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Meters;

public class VoltmeterDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "voltmeter";
    public override string DisplayName => "Voltmeter";
    public override string Category => "Meters";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "+", 0, -1, PinDirection.Up),
        new(1, "-", 0, 1, PinDirection.Down)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var vm = new VoltmeterDevice(id, x, y);
        vm.SetupDefaultPins();
        return vm;
    }

    public override string FormatLabel(Device device)
    {
        return device is VoltmeterDevice vm ? $"{FormatEngineering(vm.VoltageDrop, "V")}" : "[V]";
    }
}

public class AmmeterDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "amperemeter";
    public override string DisplayName => "Ammeter";
    public override string Category => "Meters";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "+", 0, -1, PinDirection.Up),
        new(1, "-", 0, 1, PinDirection.Down)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var am = new AmmeterDevice(id, x, y);
        am.SetupDefaultPins();
        return am;
    }

    public override string FormatLabel(Device device)
    {
        return device is AmmeterDevice am ? $"{FormatEngineering(am.Current, "A")}" : "[A]";
    }
}

public class OhmmeterDescriptor : DeviceDescriptorBase
{
    public override string TypeName => "ohmmeter";
    public override string DisplayName => "Ohmmeter";
    public override string Category => "Meters";

    public override IReadOnlyList<PinDefinition> Pins { get; } = new List<PinDefinition>
    {
        new(0, "Ω1", 0, -1, PinDirection.Up),
        new(1, "Ω2", 0, 1, PinDirection.Down)
    };

    public override Device CreateInstance(int id, double x, double y)
    {
        var ohm = new OhmmeterDevice(id, x, y);
        ohm.SetupDefaultPins();
        return ohm;
    }

    public override string FormatLabel(Device device)
    {
        if (device is OhmmeterDevice ohm)
        {
            if (ohm.MeasuredResistance >= 20e6)
                return "O.L. (Open)";
            return $"{FormatEngineering(ohm.MeasuredResistance, "Ω")}";
        }
        return "[Ω]";
    }
}

