using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Base;

public enum PinDirection
{
    Up = 0,
    Right = 1,
    Down = 2,
    Left = 3
}

public static class PinDirectionExtensions
{
    public static (int Dx, int Dy) ToVector(this PinDirection dir) => dir switch
    {
        PinDirection.Up => (0, -1),
        PinDirection.Right => (1, 0),
        PinDirection.Down => (0, 1),
        PinDirection.Left => (-1, 0),
        _ => (0, -1)
    };

    public static PinDirection Rotate(this PinDirection dir, DeviceRotation rotation)
    {
        int step = rotation switch
        {
            DeviceRotation.Up => 0,
            DeviceRotation.Right => 1,
            DeviceRotation.Down => 2,
            DeviceRotation.Left => 3,
            _ => 0
        };

        return (PinDirection)(((int)dir + step) % 4);
    }
}
