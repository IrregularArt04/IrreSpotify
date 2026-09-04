using System;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IrreSpotify.Services;

namespace IrreSpotify.ViewModels;

public partial class PlayerViewModel : ViewModelBase
{
    private readonly SpotifyService? _spotifyService;
    private readonly LibrespotService _librespotService;
    private readonly Timer _pollTimer;

    [ObservableProperty]
    private string _trackTitle = "No Playing Track";

    [ObservableProperty]
    private string _artistName = "Connect your account to start";

    [ObservableProperty]
    private string? _albumCoverUrl;

    [ObservableProperty]
    private string? _currentlyPlayingUri;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseIconText))]
    private bool _isPlaying;

    public string PlayPauseIconText => IsPlaying ? "⏸" : "▶";

    public event Action<string?, bool>? PlaybackStateChanged;

    [ObservableProperty]
    private int _progressMs;

    [ObservableProperty]
    private int _durationMs = 1;

    [ObservableProperty]
    private int _volumePercent = 80;

    [ObservableProperty]
    private bool _isShuffleEnabled;

    [ObservableProperty]
    private bool _isLibrespotRunning;

    [ObservableProperty]
    private string _deviceName = "IrreSpotify Lite";

    [ObservableProperty]
    private string _progressText = "0:00";

    [ObservableProperty]
    private string _durationText = "0:00";

    public PlayerViewModel(SpotifyService? spotifyService, LibrespotService librespotService)
    {
        _spotifyService = spotifyService;
        _librespotService = librespotService;

        _librespotService.StatusChanged += running => IsLibrespotRunning = running;
        IsLibrespotRunning = _librespotService.IsRunning;
        DeviceName = _librespotService.DeviceName;

        _pollTimer = new Timer(1000);
        _pollTimer.Elapsed += async (s, e) => await RefreshPlaybackAsync();
        _pollTimer.Start();
    }

    [RelayCommand]
    private async Task TogglePlayPauseAsync()
    {
        if (_spotifyService == null) return;
        bool success = await _spotifyService.TogglePlayPauseAsync(IsPlaying);
        if (success)
        {
            IsPlaying = !IsPlaying;
        }
    }

    [RelayCommand]
    private async Task SkipNextAsync()
    {
        if (_spotifyService == null) return;
        await _spotifyService.SkipToNextAsync();
        await RefreshPlaybackAsync();
    }

    [RelayCommand]
    private async Task SkipPreviousAsync()
    {
        if (_spotifyService == null) return;
        await _spotifyService.SkipToPreviousAsync();
        await RefreshPlaybackAsync();
    }

    [RelayCommand]
    private async Task ToggleShuffleAsync()
    {
        if (_spotifyService == null) return;
        bool newState = !IsShuffleEnabled;
        bool success = await _spotifyService.SetShuffleAsync(newState);
        if (success)
        {
            IsShuffleEnabled = newState;
        }
    }

    private bool _isUpdatingVolumeFromApi = false;

    partial void OnVolumePercentChanged(int value)
    {
        if (_isUpdatingVolumeFromApi) return;
        if (_spotifyService == null) return;

        _ = Task.Run(async () =>
        {
            await _spotifyService.SetVolumeAsync(Math.Clamp(value, 0, 100));
        });
    }

    [RelayCommand]
    private async Task SetVolumeAsync(double volume)
    {
        if (_spotifyService == null) return;
        int vol = (int)volume;
        VolumePercent = vol;
        await _spotifyService.SetVolumeAsync(vol);
    }

    public async Task RefreshPlaybackAsync()
    {
        if (_spotifyService == null) return;

        try
        {
            var playback = await _spotifyService.GetCurrentPlaybackAsync();
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (playback != null && playback.Item is SpotifyAPI.Web.FullTrack track)
                {
                    CurrentlyPlayingUri = track.Uri;
                    TrackTitle = track.Name;
                    ArtistName = string.Join(", ", track.Artists.Select(a => a.Name));
                    AlbumCoverUrl = track.Album.Images.FirstOrDefault()?.Url;
                    IsPlaying = playback.IsPlaying;
                    IsShuffleEnabled = playback.ShuffleState;
                    ProgressMs = playback.ProgressMs;
                    DurationMs = track.DurationMs > 0 ? track.DurationMs : 1;

                    if (playback.Device != null && playback.Device.VolumePercent.HasValue)
                    {
                        _isUpdatingVolumeFromApi = true;
                        VolumePercent = playback.Device.VolumePercent.Value;
                        _isUpdatingVolumeFromApi = false;
                    }

                    ProgressText = FormatTime(ProgressMs);
                    DurationText = FormatTime(DurationMs);

                    PlaybackStateChanged?.Invoke(CurrentlyPlayingUri, IsPlaying);
                }
                else
                {
                    PlaybackStateChanged?.Invoke(null, false);
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating player viewmodel: {ex.Message}");
        }
    }

    private static string FormatTime(int ms)
    {
        var ts = TimeSpan.FromMilliseconds(ms);
        return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
    }
}
