using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using FryCircuit.UI.Views;
using FryCircuit.UI.ViewModels;

namespace UiSnapshots;

public class SnapshotApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new Material.Icons.Avalonia.MaterialIconStyles(null));
        Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://UiSnapshots"))
        {
            Source = new Uri("avares://FryCircuit.UI/Styles/CircuitStyles.axaml")
        });
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        AppBuilder.Configure<SnapshotApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false // Use Skia drawing backend for high fidelity
            })
            .UseSkia()
            .WithInterFont()
            .SetupWithoutStarting();

        string outDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../snapshots"));
        Directory.CreateDirectory(outDir);

        Console.WriteLine($"Capturing snapshots to: {outDir}");

        if (args.Length > 0 && args[0].Equals("ai-test", StringComparison.OrdinalIgnoreCase))
        {
            string artifactDir = args.Length > 1 ? args[1] : outDir;
            CaptureAiInteractionTest(artifactDir);
            Console.WriteLine("AI interaction test snapshots captured successfully!");
            return;
        }

        if (args.Length > 0 && args[0].Equals("led", StringComparison.OrdinalIgnoreCase))
        {
            CaptureLedInspector(outDir);
            Console.WriteLine("LED snapshot captured successfully!");
            return;
        }

        if (args.Length > 0 && args[0].Equals("probe", StringComparison.OrdinalIgnoreCase))
        {
            CaptureWireProbingStates(outDir);
            Console.WriteLine("Wire probing snapshots captured successfully!");
            return;
        }

        if (args.Length > 0 && args[0].Equals("layout", StringComparison.OrdinalIgnoreCase))
        {
            CaptureLayoutStates(outDir);
            Console.WriteLine("Layout snapshots captured successfully!");
            return;
        }

        if (args.Length > 0 && args[0].Equals("spice", StringComparison.OrdinalIgnoreCase))
        {
            CaptureWindow("Superconducting DC SQUID Magnetometer", outDir, isFft: false, showCursors: false, openSpiceDrawer: true, filenameOverride: "spice_netlist_modal.png");
            Console.WriteLine("SPICE snapshot captured successfully!");
            return;
        }

        if (args.Length > 0 && args[0].Equals("bom", StringComparison.OrdinalIgnoreCase))
        {
            CaptureWindow("Aerospace 3-Phase Drone Inverter", outDir, isFft: false, showCursors: false, openBomDrawer: true, filenameOverride: "manufacturing_bom_modal.png");
            Console.WriteLine("BOM snapshot captured successfully!");
            return;
        }

        if (args.Length > 0 && args[0].Equals("ai", StringComparison.OrdinalIgnoreCase))
        {
            CaptureWindow("Inverting Op-Amp", outDir, isFft: false, showCursors: false, openLlmDrawer: true, filenameOverride: "ai_circuit_deck_modal.png");
            Console.WriteLine("AI Deck snapshot captured successfully!");
            return;
        }

        if (args.Length > 0 && args[0].Equals("preset", StringComparison.OrdinalIgnoreCase) && args.Length > 1)
        {
            string pName = args[1];
            CaptureWindow(pName, outDir, isFft: false, showCursors: true);
            Console.WriteLine($"Preset '{pName}' captured successfully!");
            return;
        }

        if (args.Length > 0 && args[0].Equals("fft", StringComparison.OrdinalIgnoreCase))
        {
            string pName = args.Length > 1 ? args[1] : "Tunnel Diode Microwave Oscillator";
            CaptureWindow(pName, outDir, isFft: true, showCursors: false, filenameOverride: "fft_spectrum_analyzer.png");
            Console.WriteLine($"FFT snapshot for '{pName}' captured successfully!");
            return;
        }

        string[] presetsToCapture = new[]
        {
            "Half-Wave Rectifier",
            "Resistor Voltage Divider",
            "RC Step Response",
            "LED with Current Limiter",
            "Two-Way Light Switch",
            "Inverting Op-Amp",
            "MOSFET Inverter",
            "RLC Resonant Tank",
            "Digital Logic Gates",
            "555 Timer Oscillator",
            "Transmon Qubit (Quantum JJ)",
            "Superconducting DC SQUID Magnetometer",
            "Tunnel Diode Microwave Oscillator",
            "Aerospace 3-Phase Drone Inverter",
            "Cosmic Heavy-Ion SEU Radiation Strike",
            "Drone BLDC Hall Commutation Controller",
            "GHz Microwave Bergeron Transmission Line",
            "Potentiometer Tuning & Multimeters",
            "LED Cascade & Current Limiter Ladder",
            "Pyramid Resistor Current Divider Network",
            "Digital 555 & Dual 7-Segment Counter"
        };

        foreach (var preset in presetsToCapture)
        {
            CaptureWindow(preset, outDir, isFft: false, showCursors: true);
        }

        // Also capture dedicated FFT Spectrum Analyzer snapshot
        CaptureWindow("Tunnel Diode Microwave Oscillator", outDir, isFft: true, showCursors: false, filenameOverride: "fft_spectrum_analyzer.png");
        CaptureWindow("Transmon Qubit (Quantum JJ)", outDir, isFft: true, showCursors: false, filenameOverride: "transmon_qubit_spectrum.png");
        CaptureWindow("Superconducting DC SQUID Magnetometer", outDir, isFft: true, showCursors: false, filenameOverride: "squid_microwave_spectrum.png");

        // Capture dedicated live stats ribbon snapshots (Live stats HUD)
        CaptureWindow("LED Cascade & Current Limiter Ladder", outDir, isFft: false, showCursors: false, filenameOverride: "frycircuit_live_ladder.png");
        CaptureWindow("Pyramid Resistor Current Divider Network", outDir, isFft: false, showCursors: false, filenameOverride: "frycircuit_pyramid_divider.png");
        CaptureWindow("Digital 555 & Dual 7-Segment Counter", outDir, isFft: false, showCursors: false, filenameOverride: "frycircuit_digital_counter_7seg.png");
        CaptureWindow("MOSFET Inverter", outDir, isFft: false, showCursors: true, filenameOverride: "frycircuit_probed_cmos_inverter.png");

        // Capture SPICE Netlist Drawer snapshot
        CaptureWindow("Superconducting DC SQUID Magnetometer", outDir, isFft: false, showCursors: false, openSpiceDrawer: true, filenameOverride: "spice_netlist_modal.png");

        // Capture Manufacturing Bill of Materials (BOM) Drawer snapshot
        CaptureWindow("Aerospace 3-Phase Drone Inverter", outDir, isFft: false, showCursors: false, openBomDrawer: true, filenameOverride: "manufacturing_bom_modal.png");

        // Capture AI Circuit Deck Drawer snapshot
        CaptureWindow("Inverting Op-Amp", outDir, isFft: false, showCursors: false, openLlmDrawer: true, filenameOverride: "ai_circuit_deck_modal.png");

        Console.WriteLine("All snapshots captured successfully!");
    }

    private static void CaptureWindow(string presetName, string outDir, bool isFft = false, bool showCursors = false, bool openSpiceDrawer = false, bool openBomDrawer = false, bool openLlmDrawer = false, string? filenameOverride = null)
    {
        var studio = new CircuitStudioView();
        var vm = new MainWindowViewModel();
        studio.DataContext = vm;
        var window = new Window
        {
            Width = 1380,
            Height = 850,
            DataContext = vm,
            Content = studio
        };
        FryCircuit.UI.Theming.CircuitThemeService.Instance.TrackResourceRoot(window);

        if (true)
        {
            vm.LoadPreset(presetName);
            // Run simulation steps so waveforms, node potentials and electron particles are vibrant
            for (int i = 0; i < 200; i++)
            {
                vm.StepSimulationCommand.Execute(null);
            }
            if (openSpiceDrawer)
            {
                vm.ToggleSpiceDrawerCommand.Execute(null);
            }
            if (openBomDrawer)
            {
                vm.ToggleBomDrawerCommand.Execute(null);
            }
            if (openLlmDrawer)
            {
                vm.ToggleLlmDrawerCommand.Execute(null);
            }
        }

        var osc = studio.FindControl<FryCircuit.UI.Controls.OscilloscopeControl>("Oscilloscope");
        if (osc != null)
        {
            osc.DisplayMode = isFft ? FryCircuit.UI.Controls.OscilloscopeDisplayMode.FftSpectrum : FryCircuit.UI.Controls.OscilloscopeDisplayMode.TimeDomain;
            osc.ShowCursors = showCursors;
        }

        var canvas = studio.FindControl<FryCircuit.UI.Controls.CircuitCanvas>("CanvasControl");
        if (canvas != null)
        {
            canvas.ShowCurrentAnnotations = true;
            canvas.ShowVoltageAnnotations = true;
            if (presetName == "MOSFET Inverter")
            {
                canvas.ProbedNodeCh1 = 1;
                canvas.ProbedNodeCh2 = 2;
            }
            canvas.ZoomToFit();
        }

        window.Show();

        string safeName = filenameOverride ?? (presetName.Replace(" ", "_").Replace("-", "_").ToLowerInvariant() + ".png");
        string filename = Path.Combine(outDir, safeName);

        // Capture frame using Avalonia Headless extension
        var frame = window.CaptureRenderedFrame();
#pragma warning disable CS0618
        if (frame != null)
        {
            frame.Save(filename);
            Console.WriteLine($"Saved snapshot: {filename}");
        }
        else
        {
            // Fallback: RenderTargetBitmap
            var pixelSize = new PixelSize(1280, 800);
            using var rtb = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
            rtb.Render(window);
            rtb.Save(filename);
            Console.WriteLine($"Saved snapshot (via RTB): {filename}");
        }
#pragma warning restore CS0618

        window.Close();
    }

    private static void CaptureLedInspector(string outDir)
    {
        var studio = new CircuitStudioView();
        var vm = new MainWindowViewModel();
        studio.DataContext = vm;
        var window = new Window
        {
            Width = 1380,
            Height = 850,
            DataContext = vm,
            Content = studio
        };

        if (true)
        {
            vm.LoadPreset("LED with Current Limiter");
            for (int i = 0; i < 100; i++)
            {
                vm.StepSimulationCommand.Execute(null);
            }

            var led = vm.CurrentCircuit?.Devices.OfType<FryCircuit.Core.Devices.LedDevice>().FirstOrDefault();
            if (led != null)
            {
                vm.SelectedDevice = led;
                vm.SetLedColorCommand.Execute("Cyan");
            }
        }

        var canvas = studio.FindControl<FryCircuit.UI.Controls.CircuitCanvas>("CanvasControl");
        if (canvas != null)
        {
            canvas.ShowCurrentAnnotations = true;
            canvas.ShowVoltageAnnotations = true;
            canvas.ZoomToFit();
        }

        window.Show();

        string filename = Path.Combine(outDir, "led_inspector_color_selection.png");
        var frame = window.CaptureRenderedFrame();
#pragma warning disable CS0618
        if (frame != null)
        {
            frame.Save(filename);
            Console.WriteLine($"Saved snapshot: {filename}");
        }
        else
        {
            var pixelSize = new PixelSize(1380, 850);
            using var rtb = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
            rtb.Render(window);
            rtb.Save(filename);
            Console.WriteLine($"Saved snapshot (via RTB): {filename}");
        }
#pragma warning restore CS0618

        window.Close();
    }

    private static void CaptureLayoutStates(string outDir)
    {
        // 1. All Panels Open (Default layout)
        CaptureLayoutVariant(outDir, "layout_all_panels_open.png", vm =>
        {
            vm.LoadPreset("Half-Wave Rectifier");
            for (int i = 0; i < 50; i++) vm.StepSimulationCommand.Execute(null);
            vm.IsLeftPanelOpen = true;
            vm.IsInspectorOpen = true;
            vm.IsOscilloscopeOpen = true;
            vm.IsOscilloscopeMaximized = false;
        });

        // 2. Panels Collapsed (Wide Canvas mode)
        CaptureLayoutVariant(outDir, "layout_panels_collapsed.png", vm =>
        {
            vm.LoadPreset("Half-Wave Rectifier");
            for (int i = 0; i < 50; i++) vm.StepSimulationCommand.Execute(null);
            vm.IsLeftPanelOpen = false;
            vm.IsInspectorOpen = false;
            vm.IsOscilloscopeOpen = false;
            vm.IsOscilloscopeMaximized = false;
        });

        // 3. Oscilloscope Maximized
        CaptureLayoutVariant(outDir, "layout_oscilloscope_maximized.png", vm =>
        {
            vm.LoadPreset("Half-Wave Rectifier");
            for (int i = 0; i < 50; i++) vm.StepSimulationCommand.Execute(null);
            vm.IsLeftPanelOpen = true;
            vm.IsInspectorOpen = true;
            vm.IsOscilloscopeOpen = true;
            vm.IsOscilloscopeMaximized = true;
        });
    }

    private static void CaptureWireProbingStates(string outDir)
    {
        // 1. Wire Inspector Unprobed (shows [👁️ View on Scope] button and wire connection)
        CaptureLayoutVariant(outDir, "wire_inspector_unprobed.png", vm =>
        {
            vm.LoadPreset("Half-Wave Rectifier");
            for (int i = 0; i < 50; i++) vm.StepSimulationCommand.Execute(null);
            var wire = vm.CurrentCircuit?.Wires.FirstOrDefault(w => w.NodeId != 0);
            if (wire != null)
            {
                vm.SelectedWire = wire;
                vm.DetachSelectedWireProbe();
            }
        });

        // 2. Wire Inspector Probed on CH1 (shows [👁️ Probed on CH1 (Amber)] active button and scope trace)
        CaptureLayoutVariant(outDir, "wire_inspector_probed_ch1.png", vm =>
        {
            vm.LoadPreset("Half-Wave Rectifier");
            for (int i = 0; i < 50; i++) vm.StepSimulationCommand.Execute(null);
            var wire = vm.CurrentCircuit?.Wires.FirstOrDefault(w => w.NodeId != 0);
            if (wire != null)
            {
                vm.SelectedWire = wire;
                vm.ToggleSelectedWireProbe();
            }
            for (int i = 0; i < 30; i++) vm.StepSimulationCommand.Execute(null);
        });
    }

    private static void CaptureLayoutVariant(string outDir, string filenameOnly, Action<MainWindowViewModel> configure)
    {
        var studio = new CircuitStudioView();
        var vm = new MainWindowViewModel();
        studio.DataContext = vm;
        var window = new Window
        {
            Width = 1380,
            Height = 850,
            DataContext = vm,
            Content = studio
        };

        configure(vm);

        var canvas = studio.FindControl<FryCircuit.UI.Controls.CircuitCanvas>("CanvasControl");
        if (canvas != null)
        {
            canvas.ShowCurrentAnnotations = true;
            canvas.ShowVoltageAnnotations = true;
            canvas.ZoomToFit();
        }

        window.Show();

        string filename = Path.Combine(outDir, filenameOnly);
        var frame = window.CaptureRenderedFrame();
#pragma warning disable CS0618
        if (frame != null)
        {
            frame.Save(filename);
            Console.WriteLine($"Saved snapshot: {filename}");
        }
        else
        {
            var pixelSize = new PixelSize(1380, 850);
            using var rtb = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
            rtb.Render(window);
            rtb.Save(filename);
            Console.WriteLine($"Saved snapshot (via RTB): {filename}");
        }
#pragma warning restore CS0618

        window.Close();
    }

    private static void CaptureAiInteractionTest(string outDir)
    {
        Directory.CreateDirectory(outDir);

        var studio = new CircuitStudioView();
        var vm = new MainWindowViewModel();
        studio.DataContext = vm;
        var window = new Window
        {
            Width = 1380,
            Height = 850,
            DataContext = vm,
            Content = studio
        };
        FryCircuit.UI.Theming.CircuitThemeService.Instance.TrackResourceRoot(window);
        window.Show();

        // 1. Snapshot Initial (Default Half-Wave Rectifier)
        SaveFrame(window, Path.Combine(outDir, "step1_initial_half_wave.png"));
        Console.WriteLine("Step 1 Captured: Initial Half-Wave Rectifier");

        // 2. Initialize CircuitAiTools and perform Live AI Interaction
        var aiTools = new FryCircuit.UI.AI.CircuitAiTools(() => vm, null);

        // Load Preset via AI
        aiTools.LoadPreset("555 Timer Oscillator").GetAwaiter().GetResult();
        aiTools.ControlSimulation("start", 1.5).GetAwaiter().GetResult();
        aiTools.StepSimulation(100);
        aiTools.ZoomToFit().GetAwaiter().GetResult();

        SaveFrame(window, Path.Combine(outDir, "step2_ai_loaded_555_timer.png"));
        Console.WriteLine("Step 2 Captured: AI Loaded 555 Timer Oscillator");

        // 3. Modify component parameter via AI
        aiTools.ModifyComponent("3", "resistance", 33000.0).GetAwaiter().GetResult();
        aiTools.StepSimulation(50);

        SaveFrame(window, Path.Combine(outDir, "step3_ai_modified_resistor.png"));
        Console.WriteLine("Step 3 Captured: AI Modified Timing Resistor to 33k");

        // 4. Load Custom 555 Timer Astable LED Flasher Schematic via AI LoadSchematic
        string ledFlasherJson = """
        {
          "$schema": "https://codefrydev.in/fryworld/schemas/frycircuit.v1.json",
          "version": 1,
          "name": "555 Timer Astable LED Flasher",
          "description": "555 Timer configured in astable multivibrator mode driving a current-limited LED.",
          "components": [
            {
              "id": "V1",
              "type": "vsource",
              "value": "5V",
              "x": 2.0,
              "y": 5.0,
              "pins": { "+": "vcc", "-": "gnd" }
            },
            {
              "id": "U1",
              "type": "timer555",
              "value": "20Ω",
              "x": 10.0,
              "y": 6.0,
              "pins": {
                "gnd": "gnd",
                "trig": "timing",
                "out": "out",
                "reset": "vcc",
                "ctrl": "ctrl",
                "thresh": "timing",
                "disch": "disch",
                "vcc": "vcc"
              }
            },
            {
              "id": "R1",
              "type": "resistor",
              "value": "1kΩ",
              "x": 6.0,
              "y": 4.0,
              "nodes": ["vcc", "disch"]
            },
            {
              "id": "R2",
              "type": "resistor",
              "value": "10kΩ",
              "x": 6.0,
              "y": 6.0,
              "nodes": ["disch", "timing"]
            },
            {
              "id": "C1",
              "type": "capacitor",
              "value": "10uF",
              "x": 6.0,
              "y": 8.0,
              "nodes": ["timing", "gnd"]
            },
            {
              "id": "C2",
              "type": "capacitor",
              "value": "10nF",
              "x": 12.0,
              "y": 8.0,
              "nodes": ["ctrl", "gnd"]
            },
            {
              "id": "Rload",
              "type": "resistor",
              "value": "470Ω",
              "x": 14.0,
              "y": 5.0,
              "nodes": ["out", "led_anode"]
            },
            {
              "id": "LED1",
              "type": "led",
              "value": "650nm",
              "x": 14.0,
              "y": 7.0,
              "nodes": ["led_anode", "gnd"]
            },
            {
              "id": "GND1",
              "type": "ground",
              "x": 10.0,
              "y": 9.5,
              "pins": { "gnd": "gnd" }
            }
          ]
        }
        """;

        string loadResult = aiTools.LoadSchematic(ledFlasherJson).GetAwaiter().GetResult();
        Console.WriteLine($"LoadSchematic Result: {loadResult}");
        aiTools.ControlSimulation("start", 1.5).GetAwaiter().GetResult();
        aiTools.StepSimulation(200);
        aiTools.ZoomToFit().GetAwaiter().GetResult();

        SaveFrame(window, Path.Combine(outDir, "step4_ai_loaded_555_led_flasher.png"));
        Console.WriteLine("Step 4 Captured: AI Loaded 555 Timer Astable LED Flasher via LoadSchematic");

        // 5. Load Inverting Op-Amp via AI
        aiTools.LoadPreset("Inverting Op-Amp").GetAwaiter().GetResult();
        aiTools.StepSimulation(100);
        aiTools.ZoomToFit().GetAwaiter().GetResult();

        SaveFrame(window, Path.Combine(outDir, "step5_ai_loaded_opamp.png"));
        Console.WriteLine("Step 5 Captured: AI Loaded Inverting Op-Amp");

        window.Close();
    }

    private static void SaveFrame(Window window, string filename)
    {
        var frame = window.CaptureRenderedFrame();
#pragma warning disable CS0618
        if (frame != null)
        {
            frame.Save(filename);
            Console.WriteLine($"Saved snapshot: {filename}");
        }
        else
        {
            var pixelSize = new PixelSize(1380, 850);
            using var rtb = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
            rtb.Render(window);
            rtb.Save(filename);
            Console.WriteLine($"Saved snapshot (via RTB): {filename}");
        }
#pragma warning restore CS0618
    }
}
