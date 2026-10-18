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
        DataContextChanged += OnDataContextChanged;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        AttachIfReady();
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

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            vm.StorageProvider = topLevel.StorageProvider;
        }

        var canvas = this.FindControl<Controls.CircuitCanvas>("CanvasControl");
        var scope = this.FindControl<Controls.OscilloscopeControl>("Oscilloscope");

        if (canvas != null && scope != null)
        {
            vm.AttachControls(canvas, scope);
        }
    }
}
