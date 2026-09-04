using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IrreSpotify.Models;
using IrreSpotify.Services;

namespace IrreSpotify.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public AuthService AuthService { get; }
    public LibrespotService LibrespotService { get; }
    public SpotifyService? SpotifyService { get; private set; }

    [ObservableProperty]
    private ViewModelBase _currentView;

    [ObservableProperty]
    private PlayerViewModel _playerViewModel;

    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private string _userName = "Guest User";

    [ObservableProperty]
    private string? _userAvatarUrl;

    [ObservableProperty]
    private string _librespotLog = "Librespot initialized...";

    [ObservableProperty]
    private string _clientId = string.Empty;

    [ObservableProperty]
    private string _authMessage = string.Empty;

    [ObservableProperty]
    private bool _showClientIdInput;

    public SearchViewModel SearchViewModel { get; }
    public LibraryViewModel LibraryViewModel { get; }

    public MainViewModel()
    {
        AuthService = new AuthService();
        LibrespotService = new LibrespotService();

        ClientId = AuthService.ClientId;
        ShowClientIdInput = string.IsNullOrWhiteSpace(ClientId);

        LibrespotService.LogReceived += msg => LibrespotLog = msg;
        LibrespotService.Start();

        SearchViewModel = new SearchViewModel(SpotifyService, PlayTrack);
        LibraryViewModel = new LibraryViewModel(SpotifyService, PlayContext, OpenPlaylist);
        PlayerViewModel = new PlayerViewModel(SpotifyService, LibrespotService);

        SubscribePlayerEvents();

        _currentView = SearchViewModel;

        AuthService.AuthStateChanged += async isAuth =>
        {
            IsAuthenticated = isAuth;
            if (isAuth && AuthService.SpotifyClient != null)
            {
                SpotifyService = new SpotifyService(AuthService);
                LibrespotService.Start();
                RebindViewModels();
                await OnUserAuthenticatedAsync();
            }
            else
            {
                SpotifyService = null;
                UserName = "Guest User";
                UserAvatarUrl = null;
            }
        };

        // Try restoring token asynchronously
        _ = Task.Run(async () => await AuthService.InitializeAsync());
    }

    private void SubscribePlayerEvents()
    {
        if (PlayerViewModel != null)
        {
            PlayerViewModel.PlaybackStateChanged -= OnPlaybackStateChanged;
            PlayerViewModel.PlaybackStateChanged += OnPlaybackStateChanged;
        }
    }

    private void OnPlaybackStateChanged(string? playingUri, bool isPlaying)
    {
        if (CurrentView is PlaylistDetailViewModel detail)
        {
            detail.UpdatePlaybackState(playingUri, isPlaying);
        }
        else if (CurrentView is SearchViewModel search)
        {
            search.UpdatePlaybackState(playingUri, isPlaying);
        }
    }

    public void OpenPlaylist(PlaylistItem playlist)
    {
        var detailVm = new PlaylistDetailViewModel(
            playlist, 
            SpotifyService, 
            PlayTrack, 
            PlayContext, 
            () => _ = NavigateToLibraryAsync()
        );
        CurrentView = detailVm;
        _ = Task.Run(async () =>
        {
            await detailVm.LoadPlaylistTracksAsync();
            detailVm.UpdatePlaybackState(PlayerViewModel.CurrentlyPlayingUri, PlayerViewModel.IsPlaying);
        });
    }

    private void RebindViewModels()
    {
        // Re-instantiate viewmodels with authenticated SpotifyService
        var search = new SearchViewModel(SpotifyService, PlayTrack);
        var library = new LibraryViewModel(SpotifyService, PlayContext, OpenPlaylist);
        PlayerViewModel = new PlayerViewModel(SpotifyService, LibrespotService);
        SubscribePlayerEvents();

        if (CurrentView is SearchViewModel)
        {
            CurrentView = search;
        }
        else if (CurrentView is LibraryViewModel)
        {
            CurrentView = library;
            _ = library.LoadLibraryAsync();
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        AuthMessage = "Opening Spotify Authorization page...";
        bool success = await AuthService.LoginAsync();
        if (success)
        {
            AuthMessage = string.Empty;
            await OnUserAuthenticatedAsync();
        }
        else
        {
            AuthMessage = !string.IsNullOrEmpty(AuthService.LastAuthError)
                ? $"Auth Error: {AuthService.LastAuthError}"
                : "Authentication failed. Check your Client ID in AuthService.cs";
        }
    }

    [RelayCommand]
    private void Logout()
    {
        AuthService.Logout();
    }

    [RelayCommand]
    private void NavigateToSearch()
    {
        CurrentView = new SearchViewModel(SpotifyService, PlayTrack);
    }

    [RelayCommand]
    private async Task NavigateToLibraryAsync()
    {
        var libVm = new LibraryViewModel(SpotifyService, PlayContext, OpenPlaylist);
        CurrentView = libVm;
        await libVm.LoadLibraryAsync();
    }

    public async void PlayTrack(string trackUri, string? contextUri = null, int trackIndex = -1)
    {
        if (SpotifyService == null) return;

        // If clicking the active track, toggle play/pause
        if (PlayerViewModel.CurrentlyPlayingUri == trackUri)
        {
            bool nextPlayingState = !PlayerViewModel.IsPlaying;
            OnPlaybackStateChanged(trackUri, nextPlayingState);
            await SpotifyService.TogglePlayPauseAsync(PlayerViewModel.IsPlaying);
            await Task.Delay(300);
            await PlayerViewModel.RefreshPlaybackAsync();
            return;
        }

        // Instant UI highlight before network roundtrip
        OnPlaybackStateChanged(trackUri, true);

        var device = await SpotifyService.GetTargetDeviceAsync(LibrespotService.DeviceName);
        string? deviceId = device?.Id;

        Console.WriteLine($"[MainViewModel] Playing track '{trackUri}' (index: {trackIndex}) with context '{contextUri ?? "none"}' on device '{device?.Name}' ({deviceId})");

        bool success = false;
        if (!string.IsNullOrWhiteSpace(contextUri))
        {
            // Play track in playlist context so Next/Previous skip through playlist items!
            success = await SpotifyService.PlayContextAsync(contextUri, deviceId, trackOffsetPosition: trackIndex >= 0 ? trackIndex : null, trackUri: trackUri);
        }
        else
        {
            // Single track playback fallback
            success = await SpotifyService.PlayTracksAsync(new List<string> { trackUri }, deviceId);
        }

        if (success)
        {
            await Task.Delay(500);
            await PlayerViewModel.RefreshPlaybackAsync();
        }
        else
        {
            await PlayerViewModel.RefreshPlaybackAsync();
        }
    }

    public async void PlayContext(string contextUri)
    {
        if (SpotifyService == null) return;
        var device = await SpotifyService.GetTargetDeviceAsync(LibrespotService.DeviceName);
        string? deviceId = device?.Id;
        Console.WriteLine($"[MainViewModel] Playing context '{contextUri}' on device '{device?.Name}' ({deviceId})");
        bool success = await SpotifyService.PlayContextAsync(contextUri, deviceId);
        if (success)
        {
            await Task.Delay(500);
            await PlayerViewModel.RefreshPlaybackAsync();
        }
    }

    [ObservableProperty]
    private string _customHexColor = "#1DB954";

    [RelayCommand]
    public void ChangeThemeColor(string? hexColor)
    {
        if (string.IsNullOrWhiteSpace(hexColor)) return;
        try
        {
            if (!hexColor.StartsWith('#')) hexColor = "#" + hexColor;
            var color = Avalonia.Media.Color.Parse(hexColor);
            SetThemeColor(color);
            CustomHexColor = hexColor;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Invalid color hex '{hexColor}': {ex.Message}");
        }
    }

    [RelayCommand]
    public void ApplyCustomHexColor()
    {
        ChangeThemeColor(CustomHexColor);
    }

    public static void SetThemeColor(Avalonia.Media.Color color)
    {
        byte r = (byte)Math.Min(255, color.R + 30);
        byte g = (byte)Math.Min(255, color.G + 30);
        byte b = (byte)Math.Min(255, color.B + 30);
        var hoverColor = Avalonia.Media.Color.FromArgb(color.A, r, g, b);

        byte bgR = (byte)(color.R * 0.25);
        byte bgG = (byte)(color.G * 0.25);
        byte bgB = (byte)(color.B * 0.25);
        var activeBgColor = Avalonia.Media.Color.FromArgb(255, bgR, bgG, bgB);

        var mainBrush = new Avalonia.Media.SolidColorBrush(color);
        var hoverBrush = new Avalonia.Media.SolidColorBrush(hoverColor);
        var activeBgBrush = new Avalonia.Media.SolidColorBrush(activeBgColor);

        if (Avalonia.Application.Current != null)
        {
            Avalonia.Application.Current.Resources["PrimaryColor"] = color;
            Avalonia.Application.Current.Resources["SpotifyGreenColor"] = color;
            Avalonia.Application.Current.Resources["SpotifyGreenBrush"] = mainBrush;
            Avalonia.Application.Current.Resources["PrimaryBrush"] = mainBrush;
            Avalonia.Application.Current.Resources["PrimaryHoverBrush"] = hoverBrush;
            Avalonia.Application.Current.Resources["PrimaryActiveBgBrush"] = activeBgBrush;
        }

        TrackItem.ActiveTitleColor = mainBrush;
        TrackItem.ActiveBackground = activeBgBrush;
    }

    private async Task OnUserAuthenticatedAsync()
    {
        if (SpotifyService == null) return;
        try
        {
            var user = await SpotifyService.GetCurrentUserAsync();
            if (user != null)
            {
                UserName = user.DisplayName ?? user.Id;
                UserAvatarUrl = user.Images?.FirstOrDefault()?.Url;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving user profile: {ex.Message}");
        }
    }
}
