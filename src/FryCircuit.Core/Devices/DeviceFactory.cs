using FryCircuit.Core.Devices.Base;
using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices;

/// <summary>
/// Factory forwarding to the modular DeviceRegistry for backward compatibility.
/// </summary>
public static class DeviceFactory
{
    public static Device CreateDevice(string type, int id = 0, double x = 0, double y = 0)
    {
        return DeviceRegistry.CreateDevice(type, id, x, y);
    }
}
