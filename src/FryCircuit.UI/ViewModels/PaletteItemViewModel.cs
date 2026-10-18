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
        "Passives" => "#38BDF8",
        "Sources" => "#F59E0B",
        "Semiconductors" => "#10B981",
        "Quantum & RF" => "#A78BFA",
        "Logic & ICs" => "#60A5FA",
        "Meters & Switches" => "#FB923C",
        _ => "#38BDF8"
    };

    public string AccentBackground => Category switch
    {
        "Passives" => "#1438BDF8",
        "Sources" => "#14F59E0B",
        "Semiconductors" => "#1410B981",
        "Quantum & RF" => "#14A78BFA",
        "Logic & ICs" => "#1460A5FA",
        "Meters & Switches" => "#14FB923C",
        _ => "#1438BDF8"
    };

    public string AccentBorder => Category switch
    {
        "Passives" => "#3338BDF8",
        "Sources" => "#33F59E0B",
        "Semiconductors" => "#3310B981",
        "Quantum & RF" => "#33A78BFA",
        "Logic & ICs" => "#3360A5FA",
        "Meters & Switches" => "#33FB923C",
        _ => "#3338BDF8"
    };
}
