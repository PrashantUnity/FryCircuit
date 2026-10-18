using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using FryCircuit.UI.Theming;
using FryCircuit.UI.ViewModels;
using FryWorld.Sdk.Services;
using FryWorld.Sdk.Theming;
using Material.Icons;
using Xunit;

namespace FryCircuit.Plugin.Tests;

public sealed class CircuitThemeTests
{
    [Fact]
    public void CircuitThemeTokens_DarkAndLightDefaults_HaveDistinctColors()
    {
        var dark = CircuitThemeTokens.DarkDefaults;
        var light = CircuitThemeTokens.LightDefaults;

        Assert.True(dark.IsDark);
        Assert.False(light.IsDark);

        Assert.NotEqual(dark.CanvasBg, light.CanvasBg);
        Assert.NotEqual(dark.Surface, light.Surface);
        Assert.NotEqual(dark.Text, light.Text);
        Assert.NotEqual(dark.Border, light.Border);

        // Brushes should match their respective colors
        Assert.Equal(dark.CanvasBg, ((ISolidColorBrush)dark.CanvasBgBrush).Color);
        Assert.Equal(light.CanvasBg, ((ISolidColorBrush)light.CanvasBgBrush).Color);
    }

    [Fact]
    public void CircuitThemeTokens_ToDictionary_ProducesCompleteTokenMap()
    {
        var tokens = CircuitThemeTokens.DarkDefaults;
        var dict = tokens.ToDictionary();

        Assert.NotNull(dict);
        Assert.True(dict.ContainsKey("CircuitBg"));
        Assert.True(dict.ContainsKey("CircuitBgBrush"));
        Assert.True(dict.ContainsKey("CircuitSurface"));
        Assert.True(dict.ContainsKey("CircuitSurfaceBrush"));
        Assert.True(dict.ContainsKey("CircuitPrimary"));
        Assert.True(dict.ContainsKey("CircuitPrimaryBrush"));
        Assert.True(dict.ContainsKey("CircuitAccent"));
        Assert.True(dict.ContainsKey("CircuitAccentBrush"));
        Assert.True(dict.ContainsKey("CircuitCanvasBg"));
        Assert.True(dict.ContainsKey("CircuitCanvasBgBrush"));
        Assert.True(dict.ContainsKey("CircuitCanvasDotMinorBrush"));
        Assert.True(dict.ContainsKey("CircuitCanvasDotMajorBrush"));
        Assert.True(dict.ContainsKey("CircuitWireBrush"));
        Assert.True(dict.ContainsKey("CircuitScopeGridBrush"));

        // Verify brush values are ISolidColorBrush
        Assert.IsAssignableFrom<ISolidColorBrush>(dict["CircuitBgBrush"]);
        Assert.IsAssignableFrom<ISolidColorBrush>(dict["CircuitCanvasBgBrush"]);
    }

    [Fact]
    public void CircuitThemeService_SetThemeAndToggle_UpdatesStateAndFiresEvents()
    {
        var service = CircuitThemeService.Instance;

        bool themeChangedFired = false;
        bool tokensChangedFired = false;

        AppTheme? lastTheme = null;
        CircuitThemeTokens? lastTokens = null;

        Action<AppTheme> themeHandler = t =>
        {
            themeChangedFired = true;
            lastTheme = t;
        };
        Action<CircuitThemeTokens> tokensHandler = tok =>
        {
            tokensChangedFired = true;
            lastTokens = tok;
        };

        service.SetTheme(AppTheme.Dark);
        service.ThemeChanged += themeHandler;
        service.TokensChanged += tokensHandler;

        try
        {
            // Set to Light
            service.SetTheme(AppTheme.Light);
            Assert.Equal(AppTheme.Light, service.CurrentTheme);
            Assert.False(service.IsDark);
            Assert.False(service.Tokens.IsDark);
            Assert.True(themeChangedFired);
            Assert.True(tokensChangedFired);
            Assert.Equal(AppTheme.Light, lastTheme);
            Assert.NotNull(lastTokens);
            Assert.False(lastTokens!.IsDark);

            // Reset flags
            themeChangedFired = false;
            tokensChangedFired = false;

            // Toggle back to Dark
            service.ToggleTheme();
            Assert.Equal(AppTheme.Dark, service.CurrentTheme);
            Assert.True(service.IsDark);
            Assert.True(service.Tokens.IsDark);
            Assert.True(themeChangedFired);
            Assert.True(tokensChangedFired);
            Assert.Equal(AppTheme.Dark, lastTheme);
            Assert.NotNull(lastTokens);
            Assert.True(lastTokens!.IsDark);
        }
        finally
        {
            service.ThemeChanged -= themeHandler;
            service.TokensChanged -= tokensHandler;
            // Restore default dark theme
            service.SetTheme(AppTheme.Dark);
        }
    }

    [Fact]
    public void CircuitThemeService_TrackResourceRoot_ReceivesSinglePassTokenInjection()
    {
        var service = CircuitThemeService.Instance;
        var border = new Border();

        service.TrackResourceRoot(border);

        // Verify border resource dictionary received merged dictionary
        Assert.True(border.Resources.MergedDictionaries.Count > 0);

        service.UntrackResourceRoot(border);
        Assert.Empty(border.Resources.MergedDictionaries);
    }

    [Fact]
    public void CircuitStudioTokenContributor_MetadataAndContribution()
    {
        var contributor = new CircuitStudioTokenContributor();
        Assert.Equal("circuitstudio", contributor.Id);

        var tokens = new Dictionary<string, string>();
        var themeDef = new ThemeDefinition
        {
            Id = "dark_test",
            Name = "Dark Test",
            IsDark = true,
            Colors = new Dictionary<string, string>
            {
                ["CircuitPrimaryBrush"] = "#00D4FF",
                ["CircuitAccentBrush"] = "#00FF66"
            }
        };

        contributor.ContributeTokens(themeDef, tokens);

        Assert.True(tokens.ContainsKey("circuitstudio.surfaceBrush") || tokens.ContainsKey("circuitstudio.surface"));
        Assert.True(tokens.ContainsKey("CircuitBgBrush"));
        Assert.True(tokens.ContainsKey("CircuitPrimaryBrush"));
        Assert.True(tokens.ContainsKey("CircuitAccentBrush"));
        Assert.True(tokens.ContainsKey("CircuitCanvasBgBrush"));
        Assert.True(tokens.ContainsKey("CircuitWireBrush") || tokens.ContainsKey("CircuitWireNormalBrush"));
    }

    [Fact]
    public void MainWindowViewModel_ThemeAndPlayIcons_ReturnCorrectMaterialIconKinds()
    {
        using var vm = new MainWindowViewModel();

        // Check Play/Pause icon logic - starts simulating by default
        Assert.True(vm.IsSimulating);
        Assert.Equal(MaterialIconKind.Pause, vm.PlayPauseIconKind);
        Assert.Equal("Pause", vm.PlayPauseButtonText);

        vm.ToggleSimulationCommand.Execute(null);
        Assert.False(vm.IsSimulating);
        Assert.Equal(MaterialIconKind.Play, vm.PlayPauseIconKind);
        Assert.Equal("Play", vm.PlayPauseButtonText);

        // Check Theme toggle icon logic
        CircuitThemeService.Instance.SetTheme(AppTheme.Dark);
        Assert.True(vm.IsDarkTheme);
        Assert.Equal(MaterialIconKind.WeatherSunny, vm.ThemeIconKind);
        Assert.Equal("Switch to Light Theme", vm.ThemeTooltip);

        vm.ToggleTheme();
        Assert.False(vm.IsDarkTheme);
        Assert.Equal(MaterialIconKind.WeatherNight, vm.ThemeIconKind);
        Assert.Equal("Switch to Dark Theme", vm.ThemeTooltip);

        // Restore dark theme
        vm.ToggleTheme();
        Assert.True(vm.IsDarkTheme);
    }
}
