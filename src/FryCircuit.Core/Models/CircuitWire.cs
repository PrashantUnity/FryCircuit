namespace FryCircuit.Core.Models;

public class CircuitWire
{
    public int Id { get; set; }
    public int NodeId { get; set; }
    public int FromDeviceId { get; set; }
    public int FromPinIndex { get; set; }
    public int ToDeviceId { get; set; }
    public int ToPinIndex { get; set; }

    /// <summary>
    /// Calculated orthogonal polyline vertices (in world grid coordinates) for this wire.
    /// </summary>
    public List<(double X, double Y)> PathPoints { get; set; } = new();

    public CircuitWire() { }

    public CircuitWire(int id, int nodeId, int fromDeviceId, int fromPinIndex, int toDeviceId, int toPinIndex)
    {
        Id = id;
        NodeId = nodeId;
        FromDeviceId = fromDeviceId;
        FromPinIndex = fromPinIndex;
        ToDeviceId = toDeviceId;
        ToPinIndex = toPinIndex;
    }

    public bool ConnectsToPin(int deviceId, int pinIndex)
    {
        return (FromDeviceId == deviceId && FromPinIndex == pinIndex) ||
               (ToDeviceId == deviceId && ToPinIndex == pinIndex);
    }
}
