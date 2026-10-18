using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using FryCircuit.UI.ViewModels;
using FryCircuit.UI.Views;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Models;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Services;

namespace FryCircuit.Plugin;

/// <summary>
/// FryWorld plugin registration for the Circuit Studio (SPICE-grade schematic CAD and real-time MNA simulator).
/// Contributes a full-viewport studio page, floating shell overlay, Command Palette commands, Status Bar widget, and Ribbon tab.
/// </summary>
public class CircuitPlugin : IFryPlugin
{
    public string Id => "com.fryworld.circuit";
    public string Name => "Circuit Studio";
    public Version Version => new(1, 0, 0);

    public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();

    public IReadOnlyDictionary<string, PluginSettingDefinition>? SettingsSchema =>
        new Dictionary<string, PluginSettingDefinition>
        {
            ["DefaultPreset"] = new()
            {
                Type = PluginSettingType.Select,
                Label = "Default Circuit Preset",
                Description = "Starting circuit loaded when the studio opens",
                DefaultValue = "Half-Wave Rectifier",
                Options = new()
                {
                    "Half-Wave Rectifier",
                    "Resistor Voltage Divider",
                    "RC Step Response",
                    "LED with Current Limiter",
                    "Inverting Op-Amp",
                    "555 Timer Oscillator",
                    "Transmon Qubit (Quantum JJ)",
                    "Aerospace 3-Phase Drone Inverter"
                }
            },
            ["SimulationSpeed"] = new()
            {
                Type = PluginSettingType.Double,
                Label = "Simulation Speed",
                Description = "Time-step speed multiplier",
                DefaultValue = 1.0,
                MinValue = 0.1,
                MaxValue = 4.0,
                Step = 0.1
            },
            ["ShowCurrentAnnotations"] = new()
            {
                Type = PluginSettingType.Boolean,
                Label = "Show Current Annotations",
                Description = "Display animated current flow direction arrows and branch readings",
                DefaultValue = true
            },
            ["ShowVoltageAnnotations"] = new()
            {
                Type = PluginSettingType.Boolean,
                Label = "Show Voltage Annotations",
                Description = "Display live node voltage badges",
                DefaultValue = true
            },
            ["OperatingTemperature"] = new()
            {
                Type = PluginSettingType.Select,
                Label = "Operating Temperature",
                Description = "Environmental operating temperature physics model",
                DefaultValue = "Room Temp (25 °C)",
                Options = new()
                {
                    "Quantum (15 mK)",
                    "Cryogenic (4.2 K)",
                    "Liquid N2 (77 K)",
                    "Space Shadow (150 K)",
                    "Room Temp (25 °C)",
                    "Aerospace (+125 °C)"
                }
            }
        };

    public Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default)
    {
        void NavigateToStudio()
        {
            if (ctx.TryGetService<IShellService>(out var shell))
            {
                shell.NavigateTo(Id);
            }
        }

        void OpenOverlay()
        {
            if (ctx.TryGetService<IShellService>(out var shell))
            {
                shell.ShowOverlay($"{Id}.overlay");
            }
        }

        // 1. Full-Viewport Navigation Rail Item in FryWorld host
        ctx.RegisterNavigationItem(new NavigationItemDescriptor
        {
            Id = Id,
            Title = "Circuit Studio",
            Category = "Engineering & Simulation",
            IconKind = "LightningBolt",
            AccentColor = "#00F0FF",
            Order = 40,
            ViewFactory = () =>
            {
                var vm = new MainWindowViewModel(ctx);
                ctx.RegisterDisposable(vm);
                return new CircuitStudioView { DataContext = vm };
            }
        });

        // 2. Floating Draggable Shell Overlay
        ctx.RegisterOverlay(new OverlayDescriptor
        {
            Id = $"{Id}.overlay",
            Title = "⚡ Circuit Studio",
            IconKind = "LightningBolt",
            DefaultWidth = 1200,
            DefaultHeight = 780,
            MinWidth = 800,
            MinHeight = 550,
            IsResizable = true,
            AutoOpenOnMount = false,
            ViewFactory = () =>
            {
                var vm = new MainWindowViewModel(ctx);
                ctx.RegisterDisposable(vm);
                return new CircuitStudioView { DataContext = vm };
            }
        });

        // 3. Command Palette (Ctrl+K / ⌘K)
        ctx.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "cmd.circuit.open",
            Title = "Open Circuit Studio",
            Subtitle = "Interactive SPICE-grade analog & digital schematic CAD and real-time MNA simulation",
            Category = "Engineering & Simulation",
            IconKind = "LightningBolt",
            ShortcutKey = OperatingSystem.IsMacOS() ? "Cmd+Alt+C" : "Ctrl+Alt+C",
            Command = new RelayCommand(NavigateToStudio)
        });

        ctx.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "cmd.circuit.overlay",
            Title = "Open Circuit Floating Overlay",
            Subtitle = "Launch draggable picture-in-picture circuit simulator window",
            Category = "Engineering & Simulation",
            IconKind = "DockWindow",
            Command = new RelayCommand(OpenOverlay)
        });

        // 4. Status Bar Widget (⚡ Circuit)
        ctx.RegisterStatusBarWidget(new StatusBarWidgetDescriptor
        {
            Id = "fryworld.status.circuit",
            Label = "⚡ Circuit",
            Tooltip = "Open Circuit Studio (Interactive MNA Simulation)",
            AlignRight = true,
            Order = 25,
            ClickCommand = new RelayCommand(NavigateToStudio)
        });

        // 5. Dynamic Ribbon Tab
        ctx.RegisterRibbonTab(new RibbonTabDescriptor
        {
            Id = "circuit",
            Header = "Circuit",
            Order = 50,
            Actions = new()
            {
                new RibbonActionDescriptor
                {
                    Id = "fryworld.ribbon.circuit.open",
                    GroupId = "studio",
                    Label = "Circuit Studio",
                    Tooltip = "Open Full-Viewport Circuit Studio",
                    IconKind = "LightningBolt",
                    Order = 10,
                    Command = new RelayCommand(NavigateToStudio)
                },
                new RibbonActionDescriptor
                {
                    Id = "fryworld.ribbon.circuit.overlay",
                    GroupId = "studio",
                    Label = "Floating Window",
                    Tooltip = "Open Circuit Studio as a Floating Window",
                    IconKind = "DockWindow",
                    Order = 20,
                    Command = new RelayCommand(OpenOverlay)
                }
            }
        });

        return Task.CompletedTask;
    }
}
