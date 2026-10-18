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
    }
}
