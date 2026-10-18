using Avalonia;
using Avalonia.Media;
using FryCircuit.Core.Models;

namespace FryCircuit.UI.Rendering;

public class DeviceRenderContext
{
    public double Zoom { get; set; } = 1.0;
    public double GridSize { get; set; } = 50.0;
    public bool IsPlacingGhost { get; set; } = false;
}

public interface IDeviceRenderer
{
    void Render(DrawingContext context, Device device, Point center, double unitSize, bool isSelected, DeviceRenderContext renderContext);
}
