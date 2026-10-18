using Avalonia.Controls;
using FryCircuit.UI.ViewModels;

namespace FryCircuit.Plugin.Runner;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        FryCircuit.UI.Theming.CircuitThemeService.Instance.TrackResourceRoot(this);
        DataContext = new MainWindowViewModel();
    }
}
