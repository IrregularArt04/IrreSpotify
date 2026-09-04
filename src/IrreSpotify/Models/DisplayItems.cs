using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace IrreSpotify.Models;

public partial class TrackItem : ObservableObject
{
    public string Uri { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public string DurationText { get; set; } = string.Empty;
    public ICommand? PlayCommand { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayIconText))]
    [NotifyPropertyChangedFor(nameof(RowBackground))]
    [NotifyPropertyChangedFor(nameof(TitleForeground))]
    private bool _isPlaying;

    public string PlayIconText => IsPlaying ? "⏸" : "▶";

    public static IBrush ActiveBackground { get; set; } = Brush.Parse("#1F382B");
    public static IBrush NormalBackground { get; } = Brush.Parse("#18181C");
    public static IBrush ActiveTitleColor { get; set; } = Brush.Parse("#1DB954");
    public static IBrush NormalTitleColor { get; } = Brushes.White;

    public IBrush RowBackground => IsPlaying ? ActiveBackground : NormalBackground;
    public IBrush TitleForeground => IsPlaying ? ActiveTitleColor : NormalTitleColor;

    public void RefreshPlaybackBrushes()
    {
        OnPropertyChanged(nameof(RowBackground));
        OnPropertyChanged(nameof(TitleForeground));
    }
}

public class PlaylistItem
{
    public string Id { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public int TrackCount { get; set; }
    public ICommand? PlayCommand { get; set; }
}
