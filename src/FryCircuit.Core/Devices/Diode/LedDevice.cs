namespace FryCircuit.Core.Devices;

public class LedDevice : DiodeDevice
{
    public double Wavelength
    {
        get => GetParameter("wavelength", 650.0);
        set => SetParameter("wavelength", value);
    }

    public string ColorHex => WavelengthToHex(Wavelength);
    public string ColorName => WavelengthToColorName(Wavelength);

    /// <summary>
    /// Approximated forward turn-on voltage based on photon energy: Vf ≈ hc / (q * λ) ≈ 1240 / λ
    /// White LEDs typically drop ~3.0 - 3.2V.
    /// </summary>
    public double ForwardVoltage
    {
        get
        {
            if (Wavelength <= 0.0) return 3.2; // White phosphor LED
            double vf = 1240.0 / Math.Clamp(Wavelength, 380.0, 780.0);
            return Math.Round(Math.Clamp(vf, 1.7, 3.4), 2);
        }
    }

    public LedDevice(int id = 0, double x = 0, double y = 0, double wavelength = 650.0)
        : base(id, x, y)
    {
        Type = "led";
        Wavelength = wavelength;
    }

    public static string WavelengthToHex(double nm)
    {
        if (nm <= 0.0) return "#FFFFFF"; // White
        if (nm < 430) return "#8B00FF";  // Violet (e.g. 405nm)
        if (nm < 485) return "#0080FF";  // Blue (e.g. 470nm)
        if (nm < 515) return "#00F0FF";  // Cyan (e.g. 500nm)
        if (nm < 565) return "#00E060";  // Green (e.g. 525nm)
        if (nm < 595) return "#FFD700";  // Yellow (e.g. 585nm)
        if (nm < 630) return "#FF8C00";  // Orange (e.g. 610nm)
        return "#FF2020";                // Red (e.g. 650nm)
    }

    public static string WavelengthToColorName(double nm)
    {
        if (nm <= 0.0) return "White";
        if (nm < 430) return "Violet";
        if (nm < 485) return "Blue";
        if (nm < 515) return "Cyan";
        if (nm < 565) return "Green";
        if (nm < 595) return "Yellow";
        if (nm < 630) return "Orange";
        return "Red";
    }

    public override void StampNonLinear(MnaContext ctx)
    {
        if (Pins.Count < 2) return;

        double vd = ctx.GetNodeVoltage(Pins[0].NodeId) - ctx.GetNodeVoltage(Pins[1].NodeId);
        double vTurnOn = ForwardVoltage;

        if (vd > vTurnOn)
        {
            double gd = 0.05; // 20 Ohm dynamic resistance
            double id = gd * (vd - vTurnOn);
            double ieq = id - gd * vd;
            ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, gd);
            ctx.StampCurrentSource(Pins[0].NodeId, Pins[1].NodeId, ieq);
        }
        else
        {
            ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, 1e-9);
        }
    }

    public override void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 2)
        {
            double v0 = ctx.GetNodeVoltage(Pins[0].NodeId);
            double v1 = ctx.GetNodeVoltage(Pins[1].NodeId);
            VoltageDrop = v0 - v1;
            double vf = ForwardVoltage;
            Current = VoltageDrop > vf ? (VoltageDrop - vf) * 0.05 : 0.0;
        }
    }
}
