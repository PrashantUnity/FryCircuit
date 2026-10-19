namespace FryCircuit.UI.AI;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FryCircuit.Core.IO;
using FryCircuit.UI.ViewModels;
using FryWorld.Sdk.AI;

/// <summary>
/// Provides real-time context from Circuit Studio to the FryWorld AI assistant and external MCP clients.
/// </summary>
public class CircuitAiContextProvider : IAiContextProvider
{
    private readonly Func<MainWindowViewModel?> _getViewModel;

    public string PluginId => "com.fryworld.circuit";

    public CircuitAiContextProvider(Func<MainWindowViewModel?> getViewModel)
    {
        _getViewModel = getViewModel ?? throw new ArgumentNullException(nameof(getViewModel));
    }

    public Task<AiContextSnapshot> GetCurrentContextAsync(CancellationToken cancellationToken = default)
    {
        var vm = MainWindowViewModel.ActiveInstance ?? _getViewModel();
        var circuit = vm?.CurrentCircuit;
        var circuitName = circuit?.Name ?? "Blank Schematic";
        var activeJson = vm?.ExportCircuitToLlmJson() ?? "{}";

        var metadata = new Dictionary<string, string>
        {
            ["ComponentCount"] = (circuit?.Devices.Count ?? 0).ToString(),
            ["NetCount"] = (circuit?.Nodes.Count ?? 0).ToString(),
            ["IsSimulating"] = (vm?.IsSimulating ?? false).ToString(),
            ["SimulationSpeed"] = (vm?.SimulationSpeed ?? 1.0).ToString("F1") + "x"
        };

        var snapshot = new AiContextSnapshot
        {
            PluginId = PluginId,
            Title = $"⚡ Circuit Studio ({circuitName})",
            ActiveDocumentName = $"{circuitName}.fcircuit",
            MimeType = "application/json; schema=fcircuit",
            ActiveContent = activeJson,
            SystemPromptAugmentation = LlmPromptGenerator.GenerateSystemPrompt(),
            Metadata = metadata
        };

        return Task.FromResult(snapshot);
    }
}
