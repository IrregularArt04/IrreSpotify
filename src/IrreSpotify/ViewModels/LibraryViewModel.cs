using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IrreSpotify.Models;
using IrreSpotify.Services;

namespace IrreSpotify.ViewModels;

public partial class LibraryViewModel : ViewModelBase
{
    private readonly SpotifyService? _spotifyService;
    private readonly Action<string>? _playContextAction;
    private readonly Action<PlaylistItem>? _openPlaylistAction;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Loading user library...";

    [ObservableProperty]
    private PlaylistItem? _selectedPlaylist;

    partial void OnSelectedPlaylistChanged(PlaylistItem? value)
    {
        if (value != null)
        {
            _openPlaylistAction?.Invoke(value);
            SelectedPlaylist = null;
        }
    }

    public ObservableCollection<PlaylistItem> Playlists { get; } = new();

    public LibraryViewModel(
        SpotifyService? spotifyService, 
        Action<string>? playContextAction = null,
        Action<PlaylistItem>? openPlaylistAction = null)
    {
        _spotifyService = spotifyService;
        _playContextAction = playContextAction;
        _openPlaylistAction = openPlaylistAction;
    }

    [RelayCommand]
    public async Task LoadLibraryAsync()
    {
        if (_spotifyService == null)
        {
            StatusMessage = "Please log in to view library";
            return;
        }

        IsLoading = true;
        StatusMessage = "Fetching playlists...";
        Playlists.Clear();

        try
        {
            var playlistsPaging = await _spotifyService.GetUserPlaylistsAsync();
            if (playlistsPaging?.Items != null)
            {
                foreach (var pl in playlistsPaging.Items)
                {
                    var item = new PlaylistItem
                    {
                        Id = pl.Id ?? string.Empty,
                        Uri = pl.Uri ?? string.Empty,
                        Name = pl.Name ?? "Untitled Playlist",
                        Owner = pl.Owner?.DisplayName ?? "Spotify",
                        CoverUrl = pl.Images?.FirstOrDefault()?.Url,
                        TrackCount = pl.Items?.Total ?? 0
                    };
                    item.PlayCommand = new RelayCommand(() => PlayPlaylist(item));
                    Playlists.Add(item);
                }
                StatusMessage = $"{Playlists.Count} playlists loaded";
            }
            else
            {
                StatusMessage = "No playlists found";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading library: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenPlaylist(PlaylistItem? playlist)
    {
        if (playlist != null)
        {
            _openPlaylistAction?.Invoke(playlist);
        }
    }

    [RelayCommand]
    private void PlayPlaylist(PlaylistItem? playlist)
    {
        if (playlist != null && !string.IsNullOrEmpty(playlist.Uri))
        {
            _playContextAction?.Invoke(playlist.Uri);
        }
    }
}
