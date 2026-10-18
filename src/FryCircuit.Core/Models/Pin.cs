namespace FryCircuit.Core.Models;

public class Pin
{
    public int Index { get; set; }
    public int NodeId { get; set; }
    
    /// <summary>
    /// Local X offset in grid units relative to device center (when untransformed)
    /// </summary>
    public double LocalX { get; set; }

    /// <summary>
    /// Local Y offset in grid units relative to device center (when untransformed)
    /// </summary>
    public double LocalY { get; set; }

    public Pin() { }

    public Pin(int index, int nodeId, double localX = 0, double localY = 0)
    {
        Index = index;
        NodeId = nodeId;
        LocalX = localX;
        LocalY = localY;
    }
}
