using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        LibraryViewModel = new LibraryViewModel(SpotifyService, PlayContext);
        PlayerViewModel = new PlayerViewModel(SpotifyService, LibrespotService);

        _currentView = SearchViewModel;

        AuthService.AuthStateChanged += async isAuth =>
        {
            IsAuthenticated = isAuth;
            if (isAuth && AuthService.SpotifyClient != null)
            {
                SpotifyService = new SpotifyService(AuthService);
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

    private void RebindViewModels()
    {
        // Re-instantiate viewmodels with authenticated SpotifyService
        var search = new SearchViewModel(SpotifyService, PlayTrack);
        var library = new LibraryViewModel(SpotifyService, PlayContext);
        PlayerViewModel = new PlayerViewModel(SpotifyService, LibrespotService);

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
        var libVm = new LibraryViewModel(SpotifyService, PlayContext);
        CurrentView = libVm;
        await libVm.LoadLibraryAsync();
    }

    public async void PlayTrack(string trackUri)
    {
        if (SpotifyService == null) return;
        var device = await SpotifyService.GetTargetDeviceAsync(LibrespotService.DeviceName);
        if (device != null)
        {
            await SpotifyService.PlayTracksAsync(new List<string> { trackUri }, device.Id);
            await PlayerViewModel.RefreshPlaybackAsync();
        }
    }

    public async void PlayContext(string contextUri)
    {
        if (SpotifyService == null) return;
        var device = await SpotifyService.GetTargetDeviceAsync(LibrespotService.DeviceName);
        if (device != null)
        {
            await SpotifyService.PlayContextAsync(contextUri, device.Id);
            await PlayerViewModel.RefreshPlaybackAsync();
        }
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
