using System;
using Avalonia;
using Avalonia.Controls;
using IrreSpotify.ViewModels;

namespace IrreSpotify.Views;

public partial class PlaylistDetailView : UserControl
{
    public PlaylistDetailView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is PlaylistDetailViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlaylistDetailViewModel.CurrentPage) ||
            e.PropertyName == nameof(PlaylistDetailViewModel.SelectedPageSize))
        {
            TracksScrollViewer?.ScrollToHome();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is PlaylistDetailViewModel vm)
        {
            _ = vm.LoadPlaylistTracksAsync();
        }
    }
}
