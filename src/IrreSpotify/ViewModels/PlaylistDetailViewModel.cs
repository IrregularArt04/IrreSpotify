using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IrreSpotify.Models;
using IrreSpotify.Services;
using SpotifyAPI.Web;

namespace IrreSpotify.ViewModels;

public partial class PlaylistDetailViewModel : ViewModelBase
{
    private readonly SpotifyService? _spotifyService;
    private readonly Action<string, string?, int>? _playTrackAction;
    private readonly Action<string>? _playContextAction;
    private readonly Action? _goBackAction;

    [ObservableProperty]
    private PlaylistItem _playlist;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Loading playlist tracks...";

    private readonly List<TrackItem> _allLoadedTracks = new();

    public ObservableCollection<string> PageSizeOptions { get; } = new() { "30", "60", "120", "Sin límite" };

    [ObservableProperty]
    private string _selectedPageSize = "30";

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private bool _canGoPrevious;

    [ObservableProperty]
    private bool _canGoNext;

    [ObservableProperty]
    private string _pageInfoText = string.Empty;

    [ObservableProperty]
    private string _pageNumberText = "Página 1 de 1";

    partial void OnSelectedPageSizeChanged(string value)
    {
        CurrentPage = 1;
        UpdatePage();
    }

    partial void OnCurrentPageChanged(int value)
    {
        UpdatePage();
    }

    public ObservableCollection<TrackItem> Tracks { get; } = new();

    public PlaylistDetailViewModel(
        PlaylistItem playlist,
        SpotifyService? spotifyService,
        Action<string, string?, int>? playTrackAction = null,
        Action<string>? playContextAction = null,
        Action? goBackAction = null)
    {
        _playlist = playlist;
        _spotifyService = spotifyService;
        _playTrackAction = playTrackAction;
        _playContextAction = playContextAction;
        _goBackAction = goBackAction;
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CanGoPrevious)
        {
            CurrentPage--;
        }
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CanGoNext)
        {
            CurrentPage++;
        }
    }

    private void UpdatePage()
    {
        if (_allLoadedTracks.Count == 0)
        {
            Tracks.Clear();
            TotalPages = 1;
            CanGoPrevious = false;
            CanGoNext = false;
            PageInfoText = "0 canciones";
            PageNumberText = "Página 1 de 1";
            return;
        }

        int pageSize = SelectedPageSize switch
        {
            "30" => 30,
            "60" => 60,
            "120" => 120,
            _ => int.MaxValue
        };

        if (pageSize == int.MaxValue)
        {
            TotalPages = 1;
            CurrentPage = 1;
            CanGoPrevious = false;
            CanGoNext = false;
            PageInfoText = $"Mostrando {_allLoadedTracks.Count} de {_allLoadedTracks.Count} canciones";
            PageNumberText = "Sin límite";

            Tracks.Clear();
            foreach (var t in _allLoadedTracks) Tracks.Add(t);
        }
        else
        {
            TotalPages = Math.Max(1, (int)Math.Ceiling((double)_allLoadedTracks.Count / pageSize));
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            CanGoPrevious = CurrentPage > 1;
            CanGoNext = CurrentPage < TotalPages;

            int startIndex = (CurrentPage - 1) * pageSize;
            var pageSlice = _allLoadedTracks.Skip(startIndex).Take(pageSize).ToList();

            int startDisplay = startIndex + 1;
            int endDisplay = startIndex + pageSlice.Count;

            PageInfoText = $"Mostrando {startDisplay}-{endDisplay} de {_allLoadedTracks.Count} canciones";
            PageNumberText = $"Página {CurrentPage} de {TotalPages}";

            Tracks.Clear();
            foreach (var t in pageSlice) Tracks.Add(t);
        }
    }

    public void UpdatePlaybackState(string? playingUri, bool isPlaying)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            string playingId = ExtractTrackId(playingUri);
            Console.WriteLine($"[PlaylistDetailViewModel] UpdatePlaybackState playingUri='{playingUri}' (id='{playingId}'), isPlaying={isPlaying}");

            foreach (var track in _allLoadedTracks)
            {
                string trackId = ExtractTrackId(track.Uri);
                bool isMatch = isPlaying && !string.IsNullOrEmpty(playingId) && string.Equals(trackId, playingId, StringComparison.OrdinalIgnoreCase);
                if (isMatch)
                {
                    Console.WriteLine($"[PlaylistDetailViewModel] Match found! Track='{track.Title}' (id='{trackId}')");
                }
                track.IsPlaying = isMatch;
            }
        });
    }

    private static string ExtractTrackId(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return string.Empty;
        string s = uri.Trim();
        if (s.StartsWith("spotify:track:", StringComparison.OrdinalIgnoreCase))
            s = s["spotify:track:".Length..];
        if (s.Contains("/track/"))
            s = s[(s.IndexOf("/track/") + "/track/".Length)..];
        if (s.Contains('?')) s = s.Split('?')[0];
        int colonIdx = s.LastIndexOf(':');
        if (colonIdx >= 0) s = s[(colonIdx + 1)..];
        return s;
    }

    [RelayCommand]
    public async Task LoadPlaylistTracksAsync()
    {
        string rawId = !string.IsNullOrWhiteSpace(Playlist.Id) ? Playlist.Id : Playlist.Uri;

        if (IsLoading || _allLoadedTracks.Count > 0)
        {
            return;
        }

        if (_spotifyService == null || string.IsNullOrWhiteSpace(rawId))
        {
            StatusMessage = "Cannot load playlist: missing ID or SpotifyService is null";
            return;
        }

        IsLoading = true;
        StatusMessage = "Fetching tracks...";

        try
        {
            var directTracks = await _spotifyService.GetPlaylistTracksDirectAsync(rawId, Playlist.CoverUrl);
            if (directTracks != null && directTracks.Count > 0)
            {
                _allLoadedTracks.Clear();
                foreach (var track in directTracks)
                {
                    track.PlayCommand = new RelayCommand(() => PlayTrack(track));
                    _allLoadedTracks.Add(track);
                }
                UpdatePage();
                StatusMessage = $"{_allLoadedTracks.Count} tracks loaded";
                return;
            }

            // Fallback if direct fetch is empty
            var paging = await _spotifyService.GetPlaylistItemsAsync(rawId);
            if (paging?.Items != null && paging.Items.Count > 0)
            {
                _allLoadedTracks.Clear();
                foreach (var item in paging.Items)
                {
                    if (item?.Track == null) continue;

                    if (item.Track is FullTrack track)
                    {
                        TimeSpan span = TimeSpan.FromMilliseconds(track.DurationMs);
                        string durText = span.Hours > 0
                            ? $"{span.Hours}:{span.Minutes:D2}:{span.Seconds:D2}"
                            : $"{span.Minutes}:{span.Seconds:D2}";

                        var trackItem = new TrackItem
                        {
                            Uri = track.Uri ?? string.Empty,
                            Title = track.Name ?? "Untitled Track",
                            Artist = string.Join(", ", track.Artists?.Select(a => a.Name) ?? Array.Empty<string>()),
                            Album = track.Album?.Name ?? string.Empty,
                            CoverUrl = track.Album?.Images?.FirstOrDefault()?.Url ?? Playlist.CoverUrl,
                            DurationText = durText
                        };
                        trackItem.PlayCommand = new RelayCommand(() => PlayTrack(trackItem));
                        _allLoadedTracks.Add(trackItem);
                    }
                }
                UpdatePage();
                StatusMessage = $"{_allLoadedTracks.Count} tracks loaded";
            }
            else
            {
                StatusMessage = "No tracks found in playlist";
            }
        }
        catch (APIException apiEx) when (apiEx.Response?.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            StatusMessage = "Acceso denegado (403): Esta playlist puede ser privada o restringida por Spotify.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void PlayPlaylist()
    {
        if (!string.IsNullOrEmpty(Playlist.Uri))
        {
            _playContextAction?.Invoke(Playlist.Uri);
        }
    }

    [RelayCommand]
    private async Task PlayPlaylistShuffleAsync()
    {
        if (!string.IsNullOrEmpty(Playlist.Uri) && _spotifyService != null)
        {
            await _spotifyService.SetShuffleAsync(true);
            _playContextAction?.Invoke(Playlist.Uri);
        }
    }

    [RelayCommand]
    private void PlayTrack(TrackItem? track)
    {
        if (track != null && !string.IsNullOrEmpty(track.Uri))
        {
            string playlistUri = !string.IsNullOrEmpty(Playlist.Uri) ? Playlist.Uri : Playlist.Id;
            int trackIndex = _allLoadedTracks.IndexOf(track);
            Console.WriteLine($"[PlaylistDetailViewModel] PlayTrack '{track.Title}' at index {trackIndex} (uri: '{track.Uri}') in playlist '{playlistUri}'");
            _playTrackAction?.Invoke(track.Uri, playlistUri, trackIndex);
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        _goBackAction?.Invoke();
    }
}
