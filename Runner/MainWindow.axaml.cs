using Avalonia.Controls;
using FryCircuit.UI.ViewModels;

namespace FryCircuit.Plugin.Runner;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
