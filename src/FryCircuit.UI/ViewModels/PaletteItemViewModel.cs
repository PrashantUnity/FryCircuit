namespace FryCircuit.UI.ViewModels;

/// <summary>
/// Model for a component palette item displayed in the Left Library Panel.
/// </summary>
public sealed class PaletteItemViewModel
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Symbol { get; init; }
    public required string Category { get; init; }
    public string Hotkey { get; init; } = "";
    public string ToolTip { get; init; } = "";
    public bool HasHotkey => !string.IsNullOrEmpty(Hotkey);
}
