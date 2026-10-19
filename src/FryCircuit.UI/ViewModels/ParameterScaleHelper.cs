using FryCircuit.Core.Devices.Base;

namespace FryCircuit.UI.ViewModels;

public static class ParameterScaleHelper
{
    public static List<ParameterScaleOption> GetScales(DeviceParameterSpec spec)
    {
        string unit = spec.Unit?.Trim() ?? string.Empty;
        var list = new List<ParameterScaleOption>();

        if (string.Equals(unit, "Hz", StringComparison.OrdinalIgnoreCase))
        {
            list.Add(new ParameterScaleOption("", "Hz", 1.0, "Hertz (x1)", defaultMin: 0.1, defaultMax: 1000.0, step: 0.5));
            list.Add(new ParameterScaleOption("k", "Hz", 1e3, "Kilohertz (10³ Hz)", defaultMin: 0.1, defaultMax: 1000.0, step: 0.1));
            list.Add(new ParameterScaleOption("M", "Hz", 1e6, "Megahertz (10⁶ Hz)", defaultMin: 0.1, defaultMax: 1000.0, step: 0.1));
            if (spec.Maximum >= 1e8)
            {
                list.Add(new ParameterScaleOption("G", "Hz", 1e9, "Gigahertz (10⁹ Hz)", defaultMin: 0.01, defaultMax: 10.0, step: 0.01));
            }
        }
        else if (unit == "Ω" || string.Equals(unit, "ohm", StringComparison.OrdinalIgnoreCase))
        {
            if (spec.Minimum < 1.0)
            {
                list.Add(new ParameterScaleOption("m", "Ω", 1e-3, "Milliohm (10⁻³ Ω)", defaultMin: 1.0, defaultMax: 1000.0, step: 1.0));
            }
            list.Add(new ParameterScaleOption("", "Ω", 1.0, "Ohm (x1)", defaultMin: 0.1, defaultMax: 1000.0, step: 0.5));
            list.Add(new ParameterScaleOption("k", "Ω", 1e3, "Kilohm (10³ Ω)", defaultMin: 0.1, defaultMax: 1000.0, step: 0.1));
            list.Add(new ParameterScaleOption("M", "Ω", 1e6, "Megohm (10⁶ Ω)", defaultMin: 0.01, defaultMax: 100.0, step: 0.1));
            if (spec.Maximum >= 1e9)
            {
                list.Add(new ParameterScaleOption("G", "Ω", 1e9, "Gigohm (10⁹ Ω)", defaultMin: 0.01, defaultMax: 100.0, step: 0.1));
            }
        }
        else if (string.Equals(unit, "F", StringComparison.OrdinalIgnoreCase))
        {
            list.Add(new ParameterScaleOption("p", "F", 1e-12, "Picofarad (10⁻¹² F)", defaultMin: 1.0, defaultMax: 1000.0, step: 1.0));
            list.Add(new ParameterScaleOption("n", "F", 1e-9, "Nanofarad (10⁻⁹ F)", defaultMin: 0.1, defaultMax: 1000.0, step: 0.1));
            list.Add(new ParameterScaleOption("µ", "F", 1e-6, "Microfarad (10⁻⁶ F)", defaultMin: 0.01, defaultMax: 1000.0, step: 0.1));
            list.Add(new ParameterScaleOption("m", "F", 1e-3, "Millifarad (10⁻³ F)", defaultMin: 0.01, defaultMax: 100.0, step: 0.01));
            if (spec.Maximum >= 1.0)
            {
                list.Add(new ParameterScaleOption("", "F", 1.0, "Farad (x1)", defaultMin: 0.01, defaultMax: 10.0, step: 0.01));
            }
        }
        else if (string.Equals(unit, "H", StringComparison.OrdinalIgnoreCase))
        {
            list.Add(new ParameterScaleOption("n", "H", 1e-9, "Nanohenry (10⁻⁹ H)", defaultMin: 1.0, defaultMax: 1000.0, step: 1.0));
            list.Add(new ParameterScaleOption("µ", "H", 1e-6, "Microhenry (10⁻⁶ H)", defaultMin: 0.1, defaultMax: 1000.0, step: 0.1));
            list.Add(new ParameterScaleOption("m", "H", 1e-3, "Millihenry (10⁻³ H)", defaultMin: 0.01, defaultMax: 1000.0, step: 0.1));
            list.Add(new ParameterScaleOption("", "H", 1.0, "Henry (x1)", defaultMin: 0.01, defaultMax: 100.0, step: 0.01));
        }
        else if (string.Equals(unit, "V", StringComparison.OrdinalIgnoreCase))
        {
            if (spec.Maximum >= 10.0 || Math.Abs(spec.Minimum) >= 10.0 || spec.Maximum - spec.Minimum >= 10.0)
            {
                list.Add(new ParameterScaleOption("mV", "", 1e-3, "Millivolt (10⁻³ V)", defaultMin: -1000.0, defaultMax: 1000.0, step: 1.0));
                list.Add(new ParameterScaleOption("V", "", 1.0, "Volt (x1)", defaultMin: -100.0, defaultMax: 100.0, step: 0.1));
                if (spec.Maximum >= 500.0 || spec.Minimum <= -500.0)
                {
                    list.Add(new ParameterScaleOption("kV", "", 1e3, "Kilovolt (10³ V)", defaultMin: -10.0, defaultMax: 10.0, step: 0.1));
                }
            }
        }
        else if (string.Equals(unit, "A", StringComparison.OrdinalIgnoreCase))
        {
            if (spec.Minimum <= 1e-7)
            {
                list.Add(new ParameterScaleOption("nA", "", 1e-9, "Nanoampere (10⁻⁹ A)", defaultMin: 0.1, defaultMax: 1000.0, step: 1.0));
            }
            list.Add(new ParameterScaleOption("µA", "", 1e-6, "Microampere (10⁻⁶ A)", defaultMin: 0.1, defaultMax: 1000.0, step: 0.1));
            list.Add(new ParameterScaleOption("mA", "", 1e-3, "Milliampere (10⁻³ A)", defaultMin: 0.01, defaultMax: 1000.0, step: 0.1));
            list.Add(new ParameterScaleOption("A", "", 1.0, "Ampere (x1)", defaultMin: 0.01, defaultMax: 50.0, step: 0.01));
        }
        else if (string.Equals(unit, "W", StringComparison.OrdinalIgnoreCase))
        {
            list.Add(new ParameterScaleOption("mW", "", 1e-3, "Milliwatt (10⁻³ W)", defaultMin: 0.1, defaultMax: 1000.0, step: 1.0));
            list.Add(new ParameterScaleOption("W", "", 1.0, "Watt (x1)", defaultMin: 0.01, defaultMax: 100.0, step: 0.1));
            if (spec.Maximum >= 1000.0)
            {
                list.Add(new ParameterScaleOption("kW", "", 1e3, "Kilowatt (10³ W)", defaultMin: 0.1, defaultMax: 100.0, step: 0.1));
            }
        }
        else if (string.Equals(unit, "s", StringComparison.OrdinalIgnoreCase))
        {
            list.Add(new ParameterScaleOption("ns", "", 1e-9, "Nanosecond (10⁻⁹ s)", defaultMin: 0.1, defaultMax: 1000.0, step: 0.1));
            list.Add(new ParameterScaleOption("µs", "", 1e-6, "Microsecond (10⁻⁶ s)", defaultMin: 0.1, defaultMax: 1000.0, step: 0.1));
            list.Add(new ParameterScaleOption("ms", "", 1e-3, "Millisecond (10⁻³ s)", defaultMin: 0.1, defaultMax: 1000.0, step: 0.1));
            list.Add(new ParameterScaleOption("s", "", 1.0, "Second (x1)", defaultMin: 0.01, defaultMax: 60.0, step: 0.01));
        }

        return list;
    }

    public static ParameterScaleOption? FindMatchingScale(IReadOnlyList<ParameterScaleOption> options, double value)
    {
        if (options == null || options.Count == 0) return null;
        double abs = Math.Abs(value);

        if (abs < 1e-24)
        {
            return options.FirstOrDefault(o => Math.Abs(o.Multiplier - 1.0) < 1e-9) ?? options[0];
        }

        var matched = options
            .OrderByDescending(o => o.Multiplier)
            .FirstOrDefault(o => abs >= (o.Multiplier * 0.89));

        return matched ?? options.OrderBy(o => o.Multiplier).First();
    }
}
