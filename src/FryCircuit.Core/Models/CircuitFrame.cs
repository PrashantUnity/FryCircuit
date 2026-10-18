namespace FryCircuit.Core.Models;

public class CircuitFrame
{
    public double XMin { get; set; } = 0;
    public double YMin { get; set; } = 0;
    public double XMax { get; set; } = 20;
    public double YMax { get; set; } = 20;

    public double Width => Math.Max(1, XMax - XMin);
    public double Height => Math.Max(1, YMax - YMin);

    public CircuitFrame() { }

    public CircuitFrame(double xmin, double ymin, double xmax, double ymax)
    {
        XMin = xmin;
        YMin = ymin;
        XMax = xmax;
        YMax = ymax;
    }
}

public class CircuitOptions
{
    public double Speed { get; set; } = 1.0;
    public double LogicHigh { get; set; } = 5.0;
}
