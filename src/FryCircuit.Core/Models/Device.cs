using System.ComponentModel;
using System.Runtime.CompilerServices;
using FryCircuit.Core.Devices.Base;

namespace FryCircuit.Core.Models;

public class Device : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public double LocationX { get; set; }
    public double LocationY { get; set; }
    public DeviceTransform Transform { get; set; } = new();
    public List<Pin> Pins { get; set; } = new();
    public Dictionary<string, double> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Dynamic simulation properties
    private double _current;
    public double Current
    {
        get => _current;
        set
        {
            if (Math.Abs(_current - value) > 1e-15)
            {
                _current = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Power));
                OnPropertyChanged(nameof(FormattedCurrent));
                OnPropertyChanged(nameof(FormattedPower));
            }
        }
    }

    private double _voltageDrop;
    public double VoltageDrop
    {
        get => _voltageDrop;
        set
        {
            if (Math.Abs(_voltageDrop - value) > 1e-15)
            {
                _voltageDrop = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Power));
                OnPropertyChanged(nameof(FormattedVoltage));
                OnPropertyChanged(nameof(FormattedPower));
            }
        }
    }

    public double Power => Math.Abs(Current * VoltageDrop);

    public string FormattedCurrent => FormatEngineering(Current, "A");
    public string FormattedVoltage => FormatEngineering(VoltageDrop, "V");
    public string FormattedPower => FormatEngineering(Power, "W");

    private bool _isBlown;
    public bool IsBlown
    {
        get => _isBlown;
        set
        {
            if (_isBlown != value)
            {
                _isBlown = value;
                OnPropertyChanged();
            }
        }
    }

    private string? _blownReason;
    public string? BlownReason
    {
        get => _blownReason;
        set
        {
            if (_blownReason != value)
            {
                _blownReason = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Resets/replaces a blown or fused component back to pristine functional condition.
    /// </summary>
    public virtual void ResetBlown()
    {
        IsBlown = false;
        BlownReason = null;
    }

    public static string FormatEngineering(double val, string unit)
    {
        if (double.IsNaN(val) || double.IsInfinity(val)) return $"0.00 {unit}";
        double abs = Math.Abs(val);
        if (abs < 1e-12) return $"0.00 {unit}";
        if (abs >= 1e9) return $"{(val / 1e9):F2} G{unit}";
        if (abs >= 1e6) return $"{(val / 1e6):F2} M{unit}";
        if (abs >= 1e3) return $"{(val / 1e3):F2} k{unit}";
        if (abs >= 1.0) return $"{val:F2} {unit}";
        if (abs >= 1e-3) return $"{(val * 1e3):F2} m{unit}";
        if (abs >= 1e-6) return $"{(val * 1e6):F2} µ{unit}";
        if (abs >= 1e-9) return $"{(val * 1e9):F2} n{unit}";
        return $"{(val * 1e12):F2} p{unit}";
    }

    public Device() { }

    public Device(int id, string type, double x = 0, double y = 0)
    {
        Id = id;
        Type = type;
        LocationX = x;
        LocationY = y;
    }

    public double GetParameter(string name, double defaultValue = 0.0)
    {
        return Parameters.TryGetValue(name, out var val) ? val : defaultValue;
    }

    public void SetParameter(string name, double value)
    {
        Parameters[name] = value;
    }

    /// <summary>
    /// Computes the world grid position of a pin considering device location and transform.
    /// </summary>
    public (double X, double Y) GetAbsolutePinPosition(int pinIndex)
    {
        if (pinIndex < 0 || pinIndex >= Pins.Count)
            return (LocationX, LocationY);

        var pin = Pins[pinIndex];
        var (offX, offY) = Transform.TransformOffset(pin.LocalX, pin.LocalY);
        return (LocationX + offX, LocationY + offY);
    }

    /// <summary>
    /// Computes the outward normal escape vector (dx, dy) for a pin in world space.
    /// </summary>
    public (int Dx, int Dy) GetPinNormal(int pinIndex)
    {
        if (pinIndex < 0 || pinIndex >= Pins.Count)
            return (0, -1);

        var pin = Pins[pinIndex];
        double lx = pin.LocalX;
        double ly = pin.LocalY;

        int baseDx = 0;
        int baseDy = 0;
        if (Math.Abs(ly) >= Math.Abs(lx) && Math.Abs(ly) > 0.01)
        {
            baseDy = ly < 0 ? -1 : 1;
        }
        else if (Math.Abs(lx) > 0.01)
        {
            baseDx = lx < 0 ? -1 : 1;
        }
        else
        {
            baseDy = -1;
        }

        var (tdx, tdy) = Transform.TransformOffset(baseDx, baseDy);
        return ((int)Math.Round(tdx), (int)Math.Round(tdy));
    }

    /// <summary>
    /// Gets the rectangular bounds of the device body in world grid coordinates.
    /// </summary>
    public (double MinX, double MaxX, double MinY, double MaxY) GetBoundingBox(double padding = 0.3)
    {
        return (
            LocationX - (1.0 - padding),
            LocationX + (1.0 - padding),
            LocationY - (1.0 - padding),
            LocationY + (1.0 - padding)
        );
    }

    public (double X, double Y) Center => (LocationX, LocationY);

    /// <summary>
    /// Configures default pin local offsets based on standard FryCircuit component geometry.
    /// </summary>
    public virtual void SetupDefaultPins()
    {
        if (DeviceRegistry.TryGet(Type, out var desc) && desc?.Pins != null && desc.Pins.Count > 0)
        {
            // Sync pin count and local coordinates directly from the authoritative device descriptor
            while (Pins.Count < desc.Pins.Count)
            {
                int nextIdx = Pins.Count;
                var def = desc.Pins[nextIdx];
                Pins.Add(new Pin(nextIdx, 0, def.LocalX, def.LocalY));
            }

            for (int i = 0; i < desc.Pins.Count && i < Pins.Count; i++)
            {
                Pins[i].LocalX = desc.Pins[i].LocalX;
                Pins[i].LocalY = desc.Pins[i].LocalY;
            }
            return;
        }

        // Fallback for unregistered types
        if (Pins.Count == 1)
        {
            // Ground terminal at origin (0, 0)
            Pins[0].LocalX = 0;
            Pins[0].LocalY = 0;
        }
        else if (Pins.Count == 2)
        {
            Pins[0].LocalX = 0;
            Pins[0].LocalY = -1;
            Pins[1].LocalX = 0;
            Pins[1].LocalY = 1;
        }
        else if (Pins.Count == 3)
        {
            // Transistors / Potentiometers / OpAmps / SPDT
            Pins[0].LocalX = -1;
            Pins[0].LocalY = -1;
            Pins[1].LocalX = 1;
            Pins[1].LocalY = 0;
            Pins[2].LocalX = -1;
            Pins[2].LocalY = 1;
        }
        else if (Pins.Count == 4)
        {
            // VCVS / 4-terminal
            Pins[0].LocalX = -1;
            Pins[0].LocalY = -1;
            Pins[1].LocalX = -1;
            Pins[1].LocalY = 1;
            Pins[2].LocalX = 1;
            Pins[2].LocalY = -1;
            Pins[3].LocalX = 1;
            Pins[3].LocalY = 1;
        }
    }
}
