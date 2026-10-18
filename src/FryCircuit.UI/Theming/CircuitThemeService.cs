using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using FryWorld.Sdk.Services;

namespace FryCircuit.UI.Theming;

/// <summary>
/// Core dynamic theme and token management service for FryCircuit.
/// Manages runtime theme switching (Dark & Light), custom color overrides, single-pass
/// resource dictionary swaps with ThemeLayerProvider, and multi-root synchronization.
/// </summary>
public sealed class CircuitThemeService : IThemeService
{
    private static readonly Lazy<CircuitThemeService> _lazyInstance = new(() => new CircuitThemeService());
    public static CircuitThemeService Instance => _lazyInstance.Value;

    private readonly List<IResourceDictionary> _roots = new();
    private readonly Dictionary<IResourceDictionary, ThemeLayerProvider> _installed = new();
    private readonly ThemeLayerProvider _appLayer = new();
    private bool _isAppLayerInstalled;

    private AppTheme _currentTheme = AppTheme.Dark;
    private CircuitThemeTokens _tokens = CircuitThemeTokens.DarkDefaults;

    /// <inheritdoc />
    public AppTheme CurrentTheme => _currentTheme;

    public bool IsDark => _currentTheme != AppTheme.Light;

    /// <summary>Active token definitions for programmatic drawing (Canvases, Scopes, Knobs).</summary>
    public CircuitThemeTokens Tokens => _tokens;

    /// <inheritdoc />
    public event Action<AppTheme>? ThemeChanged;

    /// <summary>Raised whenever tokens are recalculated or a theme is swapped.</summary>
    public event Action<CircuitThemeTokens>? TokensChanged;

    public CircuitThemeService()
    {
        EnsureAppLayerInstalled();
    }

    /// <inheritdoc />
    public void SetTheme(AppTheme theme)
    {
        if (_currentTheme == theme && _tokens != null) return;

        _currentTheme = theme;
        var goingDark = theme != AppTheme.Light;
        var newTokens = goingDark ? CircuitThemeTokens.DarkDefaults : CircuitThemeTokens.LightDefaults;
        ApplyTokens(newTokens);
    }

    /// <inheritdoc />
    public void ToggleTheme()
    {
        SetTheme(IsDark ? AppTheme.Light : AppTheme.Dark);
    }

    /// <summary>Applies a custom set of circuit tokens and pushes them to all tracked roots with 1 notification.</summary>
    public void ApplyTokens(CircuitThemeTokens tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        _tokens = tokens;
        _currentTheme = tokens.IsDark ? AppTheme.Dark : AppTheme.Light;

        var values = tokens.ToDictionary();

        void Commit()
        {
            EnsureAppLayerInstalled();

            if (Application.Current is { } app)
            {
                var targetVariant = tokens.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
                if (app.RequestedThemeVariant != targetVariant)
                {
                    app.RequestedThemeVariant = targetVariant;
                }
            }

            _appLayer.Replace(values);

            foreach (var root in _roots.ToList())
            {
                ApplyToRoot(root, values);
            }

            ThemeChanged?.Invoke(_currentTheme);
            TokensChanged?.Invoke(_tokens);
        }

        if (Dispatcher.UIThread.CheckAccess() || Application.Current == null)
        {
            Commit();
        }
        else
        {
            Dispatcher.UIThread.Post(Commit);
        }
    }

    /// <summary>
    /// Tracks an Avalonia styled element (such as a Window or CircuitStudioView) so theme tokens
    /// are attached directly to its resource dictionary for immediate single-pass recoloring.
    /// </summary>
    public void TrackResourceRoot(StyledElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        AttachResourceRoot(element.Resources);

        if (element is Window window)
        {
            window.Closed += (_, _) => DetachResourceRoot(window.Resources);
            return;
        }

        element.AttachedToLogicalTree += (_, _) => AttachResourceRoot(element.Resources);
        element.DetachedFromLogicalTree += (_, _) => DetachResourceRoot(element.Resources);
    }

    /// <summary>Untracks an Avalonia styled element.</summary>
    public void UntrackResourceRoot(StyledElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        DetachResourceRoot(element.Resources);
    }

    /// <summary>Attaches a resource dictionary to follow dynamic theme commits.</summary>
    public void AttachResourceRoot(IResourceDictionary root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (_roots.Contains(root)) return;
        _roots.Add(root);

        ApplyToRoot(root, _tokens.ToDictionary());
    }

    /// <summary>Detaches a resource dictionary.</summary>
    public void DetachResourceRoot(IResourceDictionary root)
    {
        ArgumentNullException.ThrowIfNull(root);
        _roots.Remove(root);
        if (_installed.Remove(root, out var layer))
        {
            root.MergedDictionaries.Remove(layer);
        }
    }

    private void EnsureAppLayerInstalled()
    {
        if (_isAppLayerInstalled) return;

        void Install()
        {
            if (Application.Current?.Resources is { } resources)
            {
                if (!resources.MergedDictionaries.Contains(_appLayer))
                {
                    resources.MergedDictionaries.Add(_appLayer);
                }
                _isAppLayerInstalled = true;
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Install();
        }
        else
        {
            Dispatcher.UIThread.Post(Install);
        }
    }

    private void ApplyToRoot(IResourceDictionary root, IReadOnlyDictionary<object, object?> values)
    {
        if (_installed.TryGetValue(root, out var layer) && root.MergedDictionaries.Contains(layer))
        {
            layer.Replace(values);
        }
        else
        {
            layer = new ThemeLayerProvider();
            layer.Replace(values);
            root.MergedDictionaries.Add(layer);
            _installed[root] = layer;
        }
    }
}
