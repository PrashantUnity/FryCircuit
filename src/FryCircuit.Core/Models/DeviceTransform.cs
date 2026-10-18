namespace FryCircuit.Core.Models;

public enum DeviceRotation
{
    Up = 0,
    Right = 90,
    Down = 180,
    Left = 270
}

public class DeviceTransform
{
    public DeviceRotation Rotation { get; set; } = DeviceRotation.Up;
    public bool Flip { get; set; } = false;

    public DeviceTransform() { }

    public DeviceTransform(DeviceRotation rotation, bool flip = false)
    {
        Rotation = rotation;
        Flip = flip;
    }

    public static DeviceRotation ParseRotation(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DeviceRotation.Up;
        return value.Trim().ToLowerInvariant() switch
        {
            "up" => DeviceRotation.Up,
            "right" => DeviceRotation.Right,
            "down" => DeviceRotation.Down,
            "left" => DeviceRotation.Left,
            _ => DeviceRotation.Up
        };
    }

    public static string RotationToString(DeviceRotation rotation) => rotation switch
    {
        DeviceRotation.Up => "up",
        DeviceRotation.Right => "right",
        DeviceRotation.Down => "down",
        DeviceRotation.Left => "left",
        _ => "up"
    };

    public (double X, double Y) TransformOffset(double localX, double localY)
    {
        double x = localX;
        double y = localY;

        if (Flip)
        {
            x = -x;
        }

        return Rotation switch
        {
            DeviceRotation.Up => (x, y),
            DeviceRotation.Right => (-y, x),
            DeviceRotation.Down => (-x, -y),
            DeviceRotation.Left => (y, -x),
            _ => (x, y)
        };
    }
}
