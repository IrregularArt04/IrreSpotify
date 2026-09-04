using Avalonia.Controls;
using IrreSpotify.ViewModels;

namespace IrreSpotify.Views;

public partial class LibraryView : UserControl
{
    public LibraryView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is LibraryViewModel vm)
        {
            _ = vm.LoadLibraryAsync();
        }
    }
}
