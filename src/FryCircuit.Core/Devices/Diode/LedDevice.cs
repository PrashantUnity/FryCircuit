namespace FryCircuit.Core.Devices;

public class LedDevice : DiodeDevice
{
    public double Wavelength
    {
        get => GetParameter("wavelength", 650.0);
        set => SetParameter("wavelength", value);
    }

    public string ColorHex => WavelengthToHex(Wavelength);

    public LedDevice(int id = 0, double x = 0, double y = 0, double wavelength = 650.0)
        : base(id, x, y)
    {
        Type = "led";
        Wavelength = wavelength;
    }

    public static string WavelengthToHex(double nm)
    {
        if (nm < 450) return "#8B00FF"; // Violet
        if (nm < 495) return "#0080FF"; // Blue
        if (nm < 570) return "#00E060"; // Green
        if (nm < 590) return "#FFD700"; // Yellow
        if (nm < 620) return "#FF8C00"; // Orange
        return "#FF2020";               // Red
    }

    public override void StampNonLinear(MnaContext ctx)
    {
        if (Pins.Count < 2) return;

        double vd = ctx.GetNodeVoltage(Pins[0].NodeId) - ctx.GetNodeVoltage(Pins[1].NodeId);
        // LEDs have a higher forward turn-on voltage ~1.8V to 2.2V
        double vTurnOn = 1.8;

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
            Current = VoltageDrop > 1.8 ? (VoltageDrop - 1.8) * 0.05 : 0.0;
        }
    }
}
