using FryCircuit.Core.Models;
using FryCircuit.Core.Physics;

namespace FryCircuit.Core.Devices;

public class LedDevice : DiodeDevice
{
    public double Wavelength
    {
        get => GetParameter("wavelength", 650.0);
        set => SetParameter("wavelength", value);
    }

    /// <summary>
    /// Maximum continuous forward current rating before the bond wire / semiconductor junction fuses (burns out).
    /// Standard 5mm LEDs are rated for 30 mA continuous (50 mA absolute max).
    /// </summary>
    public double MaxForwardCurrent
    {
        get => GetParameter("max_current", 0.030); // 30 mA
        set => SetParameter("max_current", Math.Max(0.001, value));
    }

    /// <summary>
    /// Maximum reverse breakdown voltage before irreversible avalanche junction destruction.
    /// Standard LEDs are typically rated for 5.0 V reverse voltage.
    /// </summary>
    public double MaxReverseVoltage
    {
        get => GetParameter("max_reverse_v", 5.0); // 5.0 V
        set => SetParameter("max_reverse_v", Math.Max(0.5, value));
    }

    /// <summary>
    /// Internal bulk and contact series resistance in Ohms.
    /// </summary>
    public double SeriesResistance
    {
        get => GetParameter("rs", 5.0); // 5.0 Ω
        set => SetParameter("rs", Math.Max(0.01, value));
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
        EmissionCoefficient = 2.0; // Typical for compound semiconductor LEDs (GaAsP, InGaN)
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

    private double _lastIterVd = 0.0;
    private int _lastIter = -1;

    /// <summary>
    /// Evaluates LED forward conduction with smooth exponential curve up to turn-on voltage Vf,
    /// followed by C1 continuous linear tangent extension with series resistance Rs.
    /// </summary>
    public (double Id, double Gd) EvaluateLedConduction(double vd, double vt)
    {
        double vf = ForwardVoltage;
        const double iNom = 0.020; // 20 mA at nominal forward voltage
        double rs = SeriesResistance;

        // Dynamic conductance at Vf before series resistance
        double gJunctionAtVf = iNom / vt;
        // Total dynamic conductance above Vf considering internal bulk resistance Rs
        double gMax = 1.0 / ((1.0 / gJunctionAtVf) + rs);

        if (vd > vf)
        {
            // Linear tangent with series resistance: C1 continuous
            double id = iNom + gMax * (vd - vf);
            return (id, gMax);
        }
        else if (vd >= 0.0)
        {
            // Shockley exponential conduction leading up to Vf
            double expArg = (vd - vf) / vt;
            if (expArg < -25.0) return (0.0, 1e-12);
            double expVal = Math.Exp(expArg);
            double id = iNom * expVal;
            double gd = Math.Clamp((iNom / vt) * expVal, 1e-12, gMax);
            return (id, gd);
        }
        else
        {
            // Reverse bias leakage
            return (0.0, 1e-12);
        }
    }

    public override void StampNonLinear(MnaContext ctx)
    {
        if (Pins.Count < 2) return;

        int n0 = Pins[0].NodeId;
        int n1 = Pins[1].NodeId;

        if (IsBlown)
        {
            // Fused/burned out open circuit
            ctx.StampConductance(n0, n1, 1e-12);
            return;
        }

        double vt = Math.Max(0.001, ctx.ThermalVoltage * EmissionCoefficient);
        double vcrit = ForwardVoltage;

        if (ctx.Iteration == 0 || ctx.Iteration <= _lastIter)
        {
            _lastIterVd = StateVoltage;
        }
        _lastIter = ctx.Iteration;

        double rawVd = ctx.GetNodeVoltage(n0) - ctx.GetNodeVoltage(n1);
        double vd = PnjLim(rawVd, _lastIterVd, vt, vcrit);
        _lastIterVd = vd;

        var (id, gd) = EvaluateLedConduction(vd, vt);

        double ieq = id - gd * vd;
        ctx.StampConductance(n0, n1, gd);
        ctx.StampCurrentSource(n0, n1, ieq);
    }

    public override void UpdateState(MnaContext ctx)
    {
        if (Pins.Count < 2) return;

        double v0 = ctx.GetNodeVoltage(Pins[0].NodeId);
        double v1 = ctx.GetNodeVoltage(Pins[1].NodeId);
        double vd = v0 - v1;
        VoltageDrop = vd;

        if (IsBlown)
        {
            Current = 0.0;
            return;
        }

        double vt = Math.Max(0.001, ctx.ThermalVoltage * EmissionCoefficient);
        var (id, _) = EvaluateLedConduction(vd, vt);
        Current = id;
        StateVoltage = vd;

        // Check for physical burnout conditions:
        // 1. Forward overcurrent: exceeding MaxForwardCurrent (e.g. connected to source without resistor)
        // 2. Reverse breakdown: reverse voltage exceeding MaxReverseVoltage (e.g. -12V reverse biased)
        if (id > MaxForwardCurrent)
        {
            IsBlown = true;
            BlownReason = $"Overcurrent: {FormatEngineering(id, "A")} exceeded {FormatEngineering(MaxForwardCurrent, "A")} rating";
            Current = 0.0;
        }
        else if (vd < -MaxReverseVoltage)
        {
            IsBlown = true;
            BlownReason = $"Reverse breakdown: {vd:F1} V exceeded rating (-{MaxReverseVoltage:F1} V)";
            Current = 0.0;
        }
    }
}
