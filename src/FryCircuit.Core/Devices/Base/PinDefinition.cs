namespace FryCircuit.Core.Devices.Base;

public class PinDefinition
{
    public int Index { get; set; }
    public string Name { get; set; }
    public double LocalX { get; set; }
    public double LocalY { get; set; }
    public PinDirection Direction { get; set; }

    public PinDefinition(int index, string name, double localX, double localY, PinDirection direction)
    {
        Index = index;
        Name = name;
        LocalX = localX;
        LocalY = localY;
        Direction = direction;
    }
}
