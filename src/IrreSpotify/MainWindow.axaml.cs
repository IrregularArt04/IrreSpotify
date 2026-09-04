using Avalonia.Controls;
using IrreSpotify.ViewModels;

namespace IrreSpotify;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}