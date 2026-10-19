using Avalonia;
using Avalonia.Controls;
using FryCircuit.UI.ViewModels;

namespace FryCircuit.UI.Views;

public partial class CircuitStudioView : UserControl
{
    public CircuitStudioView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        AttachIfReady();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            var canvas = this.FindControl<Controls.CircuitCanvas>("CanvasControl");
            if (canvas != null)
            {
                vm.DetachControls(canvas);
            }
        }
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        AttachIfReady();
    }

    private void AttachIfReady()
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        MainWindowViewModel.ActiveInstance = vm;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            vm.StorageProvider = topLevel.StorageProvider;
        }

        var canvas = this.FindControl<Controls.CircuitCanvas>("CanvasControl");
        var scope = this.FindControl<Controls.OscilloscopeControl>("Oscilloscope");

        if (canvas != null)
        {
            vm.AttachControls(canvas, scope);
        }
    }
}
