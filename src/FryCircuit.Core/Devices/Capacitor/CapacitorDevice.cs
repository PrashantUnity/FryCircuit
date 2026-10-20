using FryCircuit.Core.Models;
using FryCircuit.Core.Simulation;

namespace FryCircuit.Core.Devices;

public class CapacitorDevice : Device, IDeviceSimulation
{
    public double Capacitance
    {
        get => GetParameter("capacitance", 1e-6);
        set => SetParameter("capacitance", Math.Max(1e-15, value));
    }

    // Companion model state variables
    public double StateVoltage { get; set; }
    public double StateCurrent { get; set; }
    public double EquivCurrent { get; set; }

    public CapacitorDevice(int id = 0, double x = 0, double y = 0, double capacitance = 1e-6)
        : base(id, "capacitor", x, y)
    {
        Capacitance = capacitance;
        Pins = new List<Pin>
        {
            new(0, 0, 0, -1),
            new(1, 0, 0, 1)
        };
    }

    public void StampConductance(MnaContext ctx)
    {
        if (Pins.Count >= 2 && ctx.Dt > 0)
        {
            double mult = (ctx.Method == IntegrationMethod.Trapezoidal) ? 2.0 : 1.0;
            double gc = mult * Capacitance / ctx.Dt;
            ctx.StampConductance(Pins[0].NodeId, Pins[1].NodeId, gc);
        }
    }

    public void StampCurrentSources(MnaContext ctx)
    {
        if (Pins.Count >= 2 && ctx.Dt > 0)
        {
            double iEq;
            if (ctx.Method == IntegrationMethod.Trapezoidal)
            {
                double gc = 2.0 * Capacitance / ctx.Dt;
                iEq = gc * StateVoltage + StateCurrent;
            }
            else
            {
                double gc = Capacitance / ctx.Dt;
                iEq = gc * StateVoltage;
            }
            EquivCurrent = iEq;
            ctx.StampCurrentSource(Pins[1].NodeId, Pins[0].NodeId, iEq);
        }
    }

    public void StampNonLinear(MnaContext ctx) { }

    public void UpdateState(MnaContext ctx)
    {
        if (Pins.Count >= 2 && ctx.Dt > 0)
        {
            double v0 = ctx.GetNodeVoltage(Pins[0].NodeId);
            double v1 = ctx.GetNodeVoltage(Pins[1].NodeId);
            VoltageDrop = v0 - v1;
            if (ctx.Method == IntegrationMethod.Trapezoidal)
            {
                double gc = 2.0 * Capacitance / ctx.Dt;
                Current = gc * (VoltageDrop - StateVoltage) - StateCurrent;
            }
            else
            {
                Current = (Capacitance / ctx.Dt) * (VoltageDrop - StateVoltage);
            }
            StateCurrent = Current;
            StateVoltage = VoltageDrop;
        }
    }
}
