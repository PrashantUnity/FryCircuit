using Avalonia.Controls;
using FryCircuit.UI.ViewModels;

namespace FryCircuit.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var vm = new MainWindowViewModel();
        DataContext = vm;

        // Wire canvas and oscilloscope controls to ViewModel after control initialization
        var canvas = this.FindControl<Controls.CircuitCanvas>("CanvasControl");
        var scope = this.FindControl<Controls.OscilloscopeControl>("Oscilloscope");

        if (canvas != null && scope != null)
        {
            vm.AttachControls(canvas, scope);
        }
    }
}
