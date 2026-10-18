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

        if (args.Length > 0 && args[0].Equals("led", StringComparison.OrdinalIgnoreCase))
        {
            CaptureLedInspector(outDir);
            Console.WriteLine("LED snapshot captured successfully!");
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

        Console.WriteLine("All snapshots captured successfully!");
    }

    private static void CaptureWindow(string presetName, string outDir, bool isFft = false, bool showCursors = false, bool openSpiceDrawer = false, bool openBomDrawer = false, string? filenameOverride = null)
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
        }

        var osc = window.FindControl<FryCircuit.UI.Controls.OscilloscopeControl>("Oscilloscope");
        if (osc != null)
        {
            osc.DisplayMode = isFft ? FryCircuit.UI.Controls.OscilloscopeDisplayMode.FftSpectrum : FryCircuit.UI.Controls.OscilloscopeDisplayMode.TimeDomain;
            osc.ShowCursors = showCursors;
        }

        var canvas = window.FindControl<FryCircuit.UI.Controls.CircuitCanvas>("CanvasControl");
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

        var canvas = window.FindControl<FryCircuit.UI.Controls.CircuitCanvas>("CanvasControl");
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
}
