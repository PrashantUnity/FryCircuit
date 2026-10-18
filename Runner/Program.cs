using System;
using Avalonia;
using Avalonia.Themes.Fluent;
using FryCircuit.Plugin;
using FryWorld.Sdk.Runner;
using Material.Icons.Avalonia;

namespace FryCircuit.Plugin.Runner;

/// <summary>
/// Standalone runner entry point for CircuitPlugin.
/// Configures FryRunner with FluentTheme, Material icons, and inter font.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        FryRunner.Configure<CircuitPlugin>(
            app =>
            {
                app.Styles.Add(new FluentTheme());
                app.Styles.Add(new MaterialIconStyles(null));
            },
            o =>
            {
                o.Title = "FryCircuit Studio — Interactive Circuit CAD & Simulation";
                o.Width = 1420;
                o.Height = 860;
            })
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
