using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Base;

public abstract class DeviceDescriptorBase : IDeviceDescriptor
{
    public abstract string TypeName { get; }
    public abstract string DisplayName { get; }
    public abstract string Category { get; }
    public abstract IReadOnlyList<PinDefinition> Pins { get; }

    public virtual (double Width, double Height) BoundingBoxSize => (2.0, 2.0);

    public abstract Device CreateInstance(int id, double x, double y);

    public virtual string FormatLabel(Device device) => string.Empty;

    public virtual DeviceParameterSpec? PrimaryParameter => null;
    public virtual IReadOnlyList<DeviceParameterSpec> Parameters =>
        PrimaryParameter != null ? new List<DeviceParameterSpec> { PrimaryParameter } : Array.Empty<DeviceParameterSpec>();

    public virtual bool HandleClick(Device device) => false;

    public static string FormatEngineering(double val, string unit)
    {
        double abs = Math.Abs(val);
        if (abs >= 1e6) return $"{val / 1e6:F1} M{unit}";
        if (abs >= 1e3) return $"{val / 1e3:F1} k{unit}";
        if (abs >= 1.0) return $"{val:F1} {unit}";
        if (abs >= 1e-3) return $"{val * 1e3:F1} m{unit}";
        if (abs >= 1e-6) return $"{val * 1e6:F1} µ{unit}";
        if (abs >= 1e-9) return $"{val * 1e9:F1} n{unit}";
        if (abs >= 1e-12) return $"{val * 1e12:F1} p{unit}";
        if (abs >= 1e-15) return $"{val * 1e15:F1} f{unit}";
        if (abs >= 1e-18) return $"{val * 1e18:F1} a{unit}";
        return $"{val:G3} {unit}";
    }

    public static bool TryParseEngineering(string input, out double result)
    {
        result = 0.0;
        if (string.IsNullOrWhiteSpace(input)) return false;

        string s = input.Trim();
        s = s.TrimEnd('Ω', 'V', 'v', 'A', 'a', 'F', 'f', 'H', 'h', 'W', 'w', 's', 'S');
        s = s.Trim();
        if (s.EndsWith("Hz", StringComparison.OrdinalIgnoreCase))
        {
            s = s.Substring(0, s.Length - 2).Trim();
        }
        if (s.EndsWith("ohm", StringComparison.OrdinalIgnoreCase))
        {
            s = s.Substring(0, s.Length - 3).Trim();
        }

        if (string.IsNullOrWhiteSpace(s)) return false;

        char last = s[^1];
        double multiplier = 1.0;
        bool hasMultiplier = true;

        switch (last)
        {
            case 'T': case 't': multiplier = 1e12; break;
            case 'G': case 'g': multiplier = 1e9; break;
            case 'M': multiplier = 1e6; break;
            case 'k': case 'K': multiplier = 1e3; break;
            case 'm': multiplier = 1e-3; break;
            case 'u': case 'U': case 'µ': case 'μ': multiplier = 1e-6; break;
            case 'n': case 'N': multiplier = 1e-9; break;
            case 'p': case 'P': multiplier = 1e-12; break;
            case 'f': case 'F': multiplier = 1e-15; break;
            case 'a': case 'A': multiplier = 1e-18; break;
            default:
                hasMultiplier = false;
                break;
        }

        string numStr = hasMultiplier ? s.Substring(0, s.Length - 1).Trim() : s;
        if (double.TryParse(numStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double baseVal))
        {
            result = baseVal * multiplier;
            return true;
        }
        return false;
    }
}
