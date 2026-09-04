using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpotifyAPI.Web;

namespace IrreSpotify.Services;

public class SpotifyService
{
    private readonly AuthService _authService;

    public SpotifyService(AuthService authService)
    {
        _authService = authService;
    }

    private SpotifyClient Client => _authService.SpotifyClient
        ?? throw new InvalidOperationException("SpotifyClient is not initialized. Please log in first.");

    public async Task<Device?> GetTargetDeviceAsync(string deviceName = "IrreSpotify Lite")
    {
        var response = await Client.Player.GetAvailableDevices();
        return response.Devices.FirstOrDefault(d => d.Name.Equals(deviceName, StringComparison.OrdinalIgnoreCase))
            ?? response.Devices.FirstOrDefault();
    }

    public async Task<bool> TransferPlaybackAsync(string deviceId, bool play = true)
    {
        try
        {
            var request = new PlayerTransferPlaybackRequest(new List<string> { deviceId })
            {
                Play = play
            };
            return await Client.Player.TransferPlayback(request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error transferring playback: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> PlayContextAsync(string contextUri, string? targetDeviceId = null, int trackOffset = 0)
    {
        try
        {
            var request = new PlayerResumePlaybackRequest
            {
                ContextUri = contextUri,
                DeviceId = targetDeviceId
            };

            SetRequestOffset(request, trackOffset);
            return await Client.Player.ResumePlayback(request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting context playback: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> PlayTracksAsync(List<string> trackUris, string? targetDeviceId = null, int startIndex = 0)
    {
        try
        {
            var request = new PlayerResumePlaybackRequest
            {
                Uris = trackUris,
                DeviceId = targetDeviceId
            };

            SetRequestOffset(request, startIndex);
            return await Client.Player.ResumePlayback(request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error playing tracks: {ex.Message}");
            return false;
        }
    }

    private static void SetRequestOffset(PlayerResumePlaybackRequest request, int position)
    {
        var offsetType = typeof(PlayerResumePlaybackRequest).GetNestedType("Offset");
        if (offsetType != null)
        {
            var offsetInstance = Activator.CreateInstance(offsetType);
            offsetType.GetProperty("Position")?.SetValue(offsetInstance, position);
            typeof(PlayerResumePlaybackRequest).GetProperty("Offset")?.SetValue(request, offsetInstance);
        }
    }

    public async Task<bool> TogglePlayPauseAsync(bool isPlaying, string? deviceId = null)
    {
        try
        {
            if (isPlaying)
            {
                return await Client.Player.PausePlayback(new PlayerPausePlaybackRequest { DeviceId = deviceId });
            }
            else
            {
                return await Client.Player.ResumePlayback(new PlayerResumePlaybackRequest { DeviceId = deviceId });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error toggling play/pause: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SkipToNextAsync(string? deviceId = null)
    {
        try
        {
            return await Client.Player.SkipNext(new PlayerSkipNextRequest { DeviceId = deviceId });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error skipping next: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SkipToPreviousAsync(string? deviceId = null)
    {
        try
        {
            return await Client.Player.SkipPrevious(new PlayerSkipPreviousRequest { DeviceId = deviceId });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error skipping previous: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SetVolumeAsync(int volumePercent, string? deviceId = null)
    {
        try
        {
            var request = new PlayerVolumeRequest(Math.Clamp(volumePercent, 0, 100))
            {
                DeviceId = deviceId
            };
            return await Client.Player.SetVolume(request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting volume: {ex.Message}");
            return false;
        }
    }

    public async Task<CurrentlyPlayingContext?> GetCurrentPlaybackAsync()
    {
        try
        {
            return await Client.Player.GetCurrentPlayback();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting playback: {ex.Message}");
            return null;
        }
    }

    public async Task<SearchResponse?> SearchAsync(string query, int limit = 20)
    {
        try
        {
            var request = new SearchRequest(
                SearchRequest.Types.Track | SearchRequest.Types.Album | SearchRequest.Types.Playlist | SearchRequest.Types.Artist,
                query
            )
            {
                Limit = limit
            };
            return await Client.Search.Item(request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error searching: {ex.Message}");
            return null;
        }
    }

    public async Task<Paging<FullPlaylist>?> GetUserPlaylistsAsync(int limit = 50)
    {
        try
        {
            return await Client.Playlists.CurrentUsers(new PlaylistCurrentUsersRequest { Limit = limit });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching playlists: {ex.Message}");
            return null;
        }
    }

    public async Task<Paging<SavedTrack>?> GetLikedSongsAsync(int limit = 50)
    {
        try
        {
            return await Client.Library.GetTracks(new LibraryTracksRequest { Limit = limit });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching liked songs: {ex.Message}");
            return null;
        }
    }

    public async Task<PrivateUser?> GetCurrentUserAsync()
    {
        try
        {
            return await Client.UserProfile.Current();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching profile: {ex.Message}");
            return null;
        }
    }
}
