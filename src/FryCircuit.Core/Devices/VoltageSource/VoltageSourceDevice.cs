using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices;

public class VoltageSourceDevice : Device, IDeviceSimulation
{
    public VoltageWaveformType WaveformType
    {
        get => (VoltageWaveformType)(int)GetParameter("type", 0.0);
        set => SetParameter("type", (double)(int)value);
    }

    public double Voltage
    {
        get => GetParameter("voltage", GetParameter("amplitude", 5.0));
        set
        {
            SetParameter("voltage", value);
            SetParameter("amplitude", value);
        }
    }

    public double Amplitude
    {
        get => GetParameter("amplitude", 5.0);
        set => SetParameter("amplitude", value);
    }

    public double Frequency
    {
        get => GetParameter("frequency", 1000.0);
        set => SetParameter("frequency", Math.Max(0.001, value));
    }

    public double Offset
    {
        get => GetParameter("offset", 0.0);
        set => SetParameter("offset", value);
    }

    public double Phase
    {
        get => GetParameter("phase", 0.0);
        set => SetParameter("phase", value);
    }

    public VoltageSourceDevice(int id = 0, double x = 0, double y = 0, double voltage = 5.0)
        : base(id, "vsource", x, y)
    {
        Voltage = voltage;
        Pins = new List<Pin>
        {
            new(0, 0, 0, -1), // Positive pin
            new(1, 0, 0, 1)   // Negative pin
        };
    }

    public double GetInstantaneousVoltage(double time)
    {
        switch (WaveformType)
        {
            case VoltageWaveformType.Dc:
                return Voltage + Offset;

            case VoltageWaveformType.Sine:
            {
                double angle = 2.0 * Math.PI * Frequency * time + (Phase * Math.PI / 180.0);
                return Offset + Amplitude * Math.Sin(angle);
            }

            case VoltageWaveformType.Pulse:
            {
                double period = 1.0 / Frequency;
                double tInPeriod = time % period;
                if (tInPeriod < 0) tInPeriod += period;
                return tInPeriod < (period * 0.5) ? (Offset + Amplitude) : Offset;
            }

            case VoltageWaveformType.Triangle:
            {
                double period = 1.0 / Frequency;
                double tInPeriod = time % period;
                if (tInPeriod < 0) tInPeriod += period;
                double fraction = tInPeriod / period;
                double tri = fraction < 0.5 ? (4.0 * fraction - 1.0) : (3.0 - 4.0 * fraction);
                return Offset + Amplitude * tri;
            }

            default:
                return Voltage;
        }
    }

    public void StampConductance(MnaContext ctx) { }
    public void StampCurrentSources(MnaContext ctx) { }
    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 2)
        {
            double v0 = ctx.GetNodeVoltage(Pins[0].NodeId);
            double v1 = ctx.GetNodeVoltage(Pins[1].NodeId);
            VoltageDrop = v0 - v1;

            if (ctx.Solution != null)
            {
                int vsIdx = -1;
                for (int i = 0; i < ctx.VoltageSources.Count; i++)
                {
                    if (ReferenceEquals(ctx.VoltageSources[i], this))
                    {
                        vsIdx = i;
                        break;
                    }
                }

                if (vsIdx >= 0)
                {
                    int auxRow = ctx.NumNodes + vsIdx;
                    if (auxRow < ctx.Solution.Length)
                    {
                        Current = -ctx.Solution[auxRow];
                    }
                }
            }
        }
    }
}
