namespace FryCircuit.Core.Models;

public class CircuitNode
{
    public int Id { get; set; }
    public bool IsGround { get; set; }
    
    /// <summary>
    /// Calculated instantaneous voltage relative to ground (V)
    /// </summary>
    public double Voltage { get; set; }

    /// <summary>
    /// Previous time-step voltage (for numerical integration)
    /// </summary>
    public double PrevVoltage { get; set; }

    public CircuitNode() { }

    public CircuitNode(int id, bool isGround = false)
    {
        Id = id;
        IsGround = isGround;
    }
}
