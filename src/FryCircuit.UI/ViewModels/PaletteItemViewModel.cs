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

    public string AccentColor => Category switch
    {
        "Passives" => "#00F0FF",
        "Sources" => "#FFD700",
        "Semiconductors" => "#00E676",
        "Quantum & RF" => "#B388FF",
        "Logic & ICs" => "#40C4FF",
        "Meters & Switches" => "#FFAB40",
        _ => "#00F0FF"
    };

    public string AccentBackground => Category switch
    {
        "Passives" => "#1800F0FF",
        "Sources" => "#18FFD700",
        "Semiconductors" => "#1800E676",
        "Quantum & RF" => "#18B388FF",
        "Logic & ICs" => "#1840C4FF",
        "Meters & Switches" => "#18FFAB40",
        _ => "#1800F0FF"
    };

    public string AccentBorder => Category switch
    {
        "Passives" => "#3800F0FF",
        "Sources" => "#38FFD700",
        "Semiconductors" => "#3800E676",
        "Quantum & RF" => "#38B388FF",
        "Logic & ICs" => "#3840C4FF",
        "Meters & Switches" => "#38FFAB40",
        _ => "#3800F0FF"
    };
}
