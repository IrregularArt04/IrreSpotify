using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IrreSpotify.Models;
using IrreSpotify.Services;

namespace IrreSpotify.ViewModels;

public partial class SearchViewModel : ViewModelBase
{
    private readonly SpotifyService? _spotifyService;
    private readonly Action<string>? _playTrackAction;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _statusMessage = "Search for tracks, artists, or albums";

    public ObservableCollection<TrackItem> SearchResults { get; } = new();

    public SearchViewModel(SpotifyService? spotifyService, Action<string>? playTrackAction = null)
    {
        _spotifyService = spotifyService;
        _playTrackAction = playTrackAction;
    }

    [RelayCommand]
    private async Task PerformSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery) || _spotifyService == null) return;

        IsSearching = true;
        StatusMessage = "Searching Spotify...";
        SearchResults.Clear();

        try
        {
            var response = await _spotifyService.SearchAsync(SearchQuery);
            if (response?.Tracks?.Items != null && response.Tracks.Items.Count > 0)
            {
                foreach (var track in response.Tracks.Items)
                {
                    var ts = TimeSpan.FromMilliseconds(track.DurationMs);
                    SearchResults.Add(new TrackItem
                    {
                        Uri = track.Uri,
                        Title = track.Name,
                        Artist = string.Join(", ", track.Artists.Select(a => a.Name)),
                        Album = track.Album.Name,
                        CoverUrl = track.Album.Images.FirstOrDefault()?.Url ?? string.Empty,
                        DurationText = $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}"
                    });
                }
                StatusMessage = $"Found {SearchResults.Count} tracks";
            }
            else
            {
                StatusMessage = "No results found";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private void PlayTrack(TrackItem? item)
    {
        if (item != null && !string.IsNullOrEmpty(item.Uri))
        {
            _playTrackAction?.Invoke(item.Uri);
        }
    }
}
