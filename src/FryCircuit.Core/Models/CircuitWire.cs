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

    /// <summary>
    /// If true, this wire route was manually adjusted or customized by the user.
    /// Automatic router passes will preserve this path unless explicitly reset.
    /// </summary>
    public bool IsCustomPath { get; set; } = false;

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

    public List<(double X, double Y)> ClonePath()
    {
        return PathPoints.Select(p => (p.X, p.Y)).ToList();
    }

    /// <summary>
    /// Removes redundant intermediate points that lie collinear along horizontal or vertical segments,
    /// and collapses consecutive identical points.
    /// </summary>
    public void SimplifyCollinear()
    {
        if (PathPoints.Count < 2) return;

        // 1. First pass: remove consecutive identical points
        var deduped = new List<(double X, double Y)> { PathPoints[0] };
        for (int i = 1; i < PathPoints.Count; i++)
        {
            var prev = deduped[^1];
            var curr = PathPoints[i];
            if (Math.Abs(prev.X - curr.X) > 1e-4 || Math.Abs(prev.Y - curr.Y) > 1e-4)
            {
                deduped.Add(curr);
            }
        }

        if (deduped.Count < 3)
        {
            PathPoints = deduped;
            return;
        }

        // 2. Second pass: remove collinear points
        var cleaned = new List<(double X, double Y)> { deduped[0] };
        for (int i = 1; i < deduped.Count - 1; i++)
        {
            var prev = cleaned[^1];
            var curr = deduped[i];
            var next = deduped[i + 1];

            bool isCollinearH = Math.Abs(prev.Y - curr.Y) < 1e-4 && Math.Abs(curr.Y - next.Y) < 1e-4;
            bool isCollinearV = Math.Abs(prev.X - curr.X) < 1e-4 && Math.Abs(curr.X - next.X) < 1e-4;

            bool canCollapse = false;
            if (isCollinearH)
            {
                // Must be monotone in X (same direction, not a 180-deg reversal/U-turn)
                int dir1 = Math.Sign(curr.X - prev.X);
                int dir2 = Math.Sign(next.X - curr.X);
                canCollapse = (dir1 == dir2 || dir1 == 0 || dir2 == 0);
            }
            else if (isCollinearV)
            {
                // Must be monotone in Y (same direction, not a 180-deg reversal/U-turn)
                int dir1 = Math.Sign(curr.Y - prev.Y);
                int dir2 = Math.Sign(next.Y - curr.Y);
                canCollapse = (dir1 == dir2 || dir1 == 0 || dir2 == 0);
            }

            if (!canCollapse)
            {
                cleaned.Add(curr);
            }
        }
        cleaned.Add(deduped[^1]);
        PathPoints = cleaned;
    }
}

