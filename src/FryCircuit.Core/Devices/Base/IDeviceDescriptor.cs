using FryCircuit.Core.Models;

namespace FryCircuit.Core.Devices.Base;

public interface IDeviceDescriptor
{
    string TypeName { get; }
    string DisplayName { get; }
    string Category { get; }
    IReadOnlyList<PinDefinition> Pins { get; }
    (double Width, double Height) BoundingBoxSize { get; }

    Device CreateInstance(int id, double x, double y);
    string FormatLabel(Device device);
    DeviceParameterSpec? PrimaryParameter { get; }
    IReadOnlyList<DeviceParameterSpec> Parameters { get; }
    bool HandleClick(Device device);
}
