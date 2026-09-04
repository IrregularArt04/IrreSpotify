using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using OffsetParam = SpotifyAPI.Web.PlayerResumePlaybackRequest.Offset;

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

    public async Task<bool> PlayContextAsync(string contextUri, string? targetDeviceId = null, int? trackOffsetPosition = null, string? trackUri = null)
    {
        try
        {
            if (string.IsNullOrEmpty(targetDeviceId))
            {
                var device = await GetTargetDeviceAsync();
                targetDeviceId = device?.Id;
            }

            string cleanContextUri = EnsurePlaylistUri(contextUri);
            string? token = _authService.AccessToken;

            Console.WriteLine($"[SpotifyService] PlayContextAsync contextUri='{cleanContextUri}', targetDeviceId='{targetDeviceId}', offsetPos={trackOffsetPosition}, trackUri='{trackUri}'");

            if (!string.IsNullOrEmpty(token))
            {
                using var http = new System.Net.Http.HttpClient();
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                object bodyObj;
                if (trackOffsetPosition.HasValue && trackOffsetPosition.Value >= 0)
                {
                    bodyObj = new
                    {
                        context_uri = cleanContextUri,
                        offset = new { position = trackOffsetPosition.Value }
                    };
                }
                else if (!string.IsNullOrEmpty(trackUri))
                {
                    bodyObj = new
                    {
                        context_uri = cleanContextUri,
                        offset = new { uri = trackUri }
                    };
                }
                else
                {
                    bodyObj = new
                    {
                        context_uri = cleanContextUri
                    };
                }

                string jsonBody = JsonSerializer.Serialize(bodyObj);
                string url = "https://api.spotify.com/v1/me/player/play";
                if (!string.IsNullOrEmpty(targetDeviceId))
                {
                    url += $"?device_id={Uri.EscapeDataString(targetDeviceId)}";
                }

                Console.WriteLine($"[SpotifyService] Direct HTTP PUT {url} -> Body: {jsonBody}");

                var content = new System.Net.Http.StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
                var response = await http.PutAsync(url, content);
                string respText = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[SpotifyService] HTTP Status: {response.StatusCode}, Response: '{respText}'");

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }

            // Fallback to SpotifyAPI.Web client if direct HTTP call is skipped
            var request = new PlayerResumePlaybackRequest
            {
                ContextUri = cleanContextUri,
                DeviceId = targetDeviceId
            };
            if (trackOffsetPosition.HasValue && trackOffsetPosition.Value >= 0)
            {
                SetRequestOffsetPosition(request, trackOffsetPosition.Value);
            }
            else if (!string.IsNullOrEmpty(trackUri))
            {
                SetRequestOffsetUri(request, trackUri);
            }
            return await Client.Player.ResumePlayback(request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting context playback: {ex.Message}");
            return false;
        }
    }

    public static string EnsurePlaylistUri(string uriOrId)
    {
        if (string.IsNullOrWhiteSpace(uriOrId)) return string.Empty;
        string s = uriOrId.Trim();
        if (s.StartsWith("spotify:", StringComparison.OrdinalIgnoreCase)) return s;
        if (s.Contains("/playlist/"))
            s = s[(s.IndexOf("/playlist/") + "/playlist/".Length)..];
        if (s.Contains('?')) s = s.Split('?')[0];
        int colonIdx = s.LastIndexOf(':');
        if (colonIdx >= 0) s = s[(colonIdx + 1)..];
        return $"spotify:playlist:{s}";
    }

    public async Task<bool> PlayTracksAsync(List<string> trackUris, string? targetDeviceId = null, int startIndex = 0)
    {
        try
        {
            if (string.IsNullOrEmpty(targetDeviceId))
            {
                var device = await GetTargetDeviceAsync();
                targetDeviceId = device?.Id;
            }

            var request = new PlayerResumePlaybackRequest
            {
                Uris = trackUris,
                DeviceId = targetDeviceId
            };

            if (startIndex > 0)
            {
                SetRequestOffsetPosition(request, startIndex);
            }

            return await Client.Player.ResumePlayback(request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error playing tracks: {ex.Message}");
            return false;
        }
    }

    private static void SetRequestOffsetPosition(PlayerResumePlaybackRequest request, int position)
    {
        var offsetObj = new PlayerResumePlaybackRequest.Offset
        {
            Position = position
        };
        typeof(PlayerResumePlaybackRequest).GetProperty("Offset")?.SetValue(request, offsetObj);
    }

    private static void SetRequestOffsetUri(PlayerResumePlaybackRequest request, string trackUri)
    {
        var offsetObj = new PlayerResumePlaybackRequest.Offset
        {
            Uri = trackUri
        };
        typeof(PlayerResumePlaybackRequest).GetProperty("Offset")?.SetValue(request, offsetObj);
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

    public async Task<bool> SetVolumeAsync(int volumePercent, string? targetDeviceId = null)
    {
        try
        {
            int vol = Math.Clamp(volumePercent, 0, 100);

            if (string.IsNullOrEmpty(targetDeviceId))
            {
                var device = await GetTargetDeviceAsync();
                targetDeviceId = device?.Id;
            }

            string? token = _authService.AccessToken;
            if (!string.IsNullOrEmpty(token))
            {
                using var http = new System.Net.Http.HttpClient();
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                string url = $"https://api.spotify.com/v1/me/player/volume?volume_percent={vol}";
                if (!string.IsNullOrEmpty(targetDeviceId))
                {
                    url += $"&device_id={Uri.EscapeDataString(targetDeviceId)}";
                }

                var content = new System.Net.Http.StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");
                var response = await http.PutAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }

            // Fallback to SpotifyAPI.Web client
            var request = new PlayerVolumeRequest(vol)
            {
                DeviceId = targetDeviceId
            };
            return await Client.Player.SetVolume(request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting volume: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SetShuffleAsync(bool state, string? targetDeviceId = null)
    {
        try
        {
            if (string.IsNullOrEmpty(targetDeviceId))
            {
                var device = await GetTargetDeviceAsync();
                targetDeviceId = device?.Id;
            }

            string? token = _authService.AccessToken;
            if (!string.IsNullOrEmpty(token))
            {
                using var http = new System.Net.Http.HttpClient();
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                string url = $"https://api.spotify.com/v1/me/player/shuffle?state={state.ToString().ToLower()}";
                if (!string.IsNullOrEmpty(targetDeviceId))
                {
                    url += $"&device_id={Uri.EscapeDataString(targetDeviceId)}";
                }

                var content = new System.Net.Http.StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");
                var response = await http.PutAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }

            var request = new PlayerShuffleRequest(state)
            {
                DeviceId = targetDeviceId
            };
            return await Client.Player.SetShuffle(request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting shuffle: {ex.Message}");
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

    public async Task<List<Models.TrackItem>> GetPlaylistTracksDirectAsync(string playlistId, string? defaultCoverUrl = null)
    {
        var list = new List<Models.TrackItem>();
        try
        {
            if (string.IsNullOrWhiteSpace(playlistId)) return list;

            string cleanId = playlistId.Trim();
            if (cleanId.StartsWith("spotify:playlist:", StringComparison.OrdinalIgnoreCase))
                cleanId = cleanId["spotify:playlist:".Length..];
            if (cleanId.Contains("/playlist/"))
                cleanId = cleanId[(cleanId.IndexOf("/playlist/") + "/playlist/".Length)..];
            if (cleanId.Contains('?')) cleanId = cleanId.Split('?')[0];
            int colonIdx = cleanId.LastIndexOf(':');
            if (colonIdx >= 0) cleanId = cleanId[(colonIdx + 1)..];

            string token = _authService.CurrentToken?.AccessToken ?? "";
            if (string.IsNullOrEmpty(token)) return list;

            using var http = new System.Net.Http.HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("IrreSpotify/1.0");
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            string? nextUrl = $"https://api.spotify.com/v1/playlists/{cleanId}";
            bool isFirstTry = true;

            while (!string.IsNullOrEmpty(nextUrl))
            {
                var resp = await http.GetAsync(nextUrl);
                if (!resp.IsSuccessStatusCode)
                {
                    string errBody = await resp.Content.ReadAsStringAsync();
                    Console.WriteLine($"[SpotifyService] Direct HTTP GET {nextUrl} returned status {resp.StatusCode}: '{errBody}'");

                    if (isFirstTry)
                    {
                        isFirstTry = false;
                        nextUrl = $"https://api.spotify.com/v1/playlists/{cleanId}/tracks?limit=100";
                        continue;
                    }
                    break;
                }
                isFirstTry = false;

                string json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                JsonElement pagingObj = default;
                JsonElement itemsProp = default;

                if (root.TryGetProperty("items", out var itemsObj))
                {
                    if (itemsObj.ValueKind == JsonValueKind.Object)
                    {
                        pagingObj = itemsObj;
                        if (itemsObj.TryGetProperty("items", out var innerArr)) itemsProp = innerArr;
                    }
                    else if (itemsObj.ValueKind == JsonValueKind.Array)
                    {
                        itemsProp = itemsObj;
                        pagingObj = root;
                    }
                }
                else if (root.TryGetProperty("tracks", out var tracksObj))
                {
                    if (tracksObj.ValueKind == JsonValueKind.Object)
                    {
                        pagingObj = tracksObj;
                        if (tracksObj.TryGetProperty("items", out var innerArr)) itemsProp = innerArr;
                    }
                    else if (tracksObj.ValueKind == JsonValueKind.Array)
                    {
                        itemsProp = tracksObj;
                    }
                }

                if (itemsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in itemsProp.EnumerateArray())
                    {
                        JsonElement trackProp = default;
                        if (item.TryGetProperty("track", out var tp) && tp.ValueKind == JsonValueKind.Object)
                        {
                            trackProp = tp;
                        }
                        else if (item.TryGetProperty("item", out var ip) && ip.ValueKind == JsonValueKind.Object)
                        {
                            trackProp = ip;
                        }
                        else if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("name", out _))
                        {
                            trackProp = item;
                        }

                        if (trackProp.ValueKind != JsonValueKind.Object) continue;

                        string uri = trackProp.TryGetProperty("uri", out var u) ? u.GetString() ?? "" : "";
                        if (string.IsNullOrEmpty(uri)) continue;

                        string title = trackProp.TryGetProperty("name", out var n) ? n.GetString() ?? "Untitled Track" : "Untitled Track";

                        string artist = "";
                        if (trackProp.TryGetProperty("artists", out var artistsArray) && artistsArray.ValueKind == JsonValueKind.Array)
                        {
                            var artistNames = new List<string>();
                            foreach (var a in artistsArray.EnumerateArray())
                            {
                                if (a.TryGetProperty("name", out var an)) artistNames.Add(an.GetString() ?? "");
                            }
                            artist = string.Join(", ", artistNames);
                        }

                        string album = "";
                        string? coverUrl = null;
                        if (trackProp.TryGetProperty("album", out var albumProp))
                        {
                            if (albumProp.TryGetProperty("name", out var aln)) album = aln.GetString() ?? "";
                            if (albumProp.TryGetProperty("images", out var imgArray) && imgArray.ValueKind == JsonValueKind.Array)
                            {
                                var firstImg = imgArray.EnumerateArray().FirstOrDefault();
                                if (firstImg.ValueKind != JsonValueKind.Undefined && firstImg.TryGetProperty("url", out var imgUrl))
                                {
                                    coverUrl = imgUrl.GetString();
                                }
                            }
                        }

                        int durMs = trackProp.TryGetProperty("duration_ms", out var dMs) ? dMs.GetInt32() : 0;
                        TimeSpan span = TimeSpan.FromMilliseconds(durMs);
                        string durText = span.Hours > 0
                            ? $"{span.Hours}:{span.Minutes:D2}:{span.Seconds:D2}"
                            : $"{span.Minutes}:{span.Seconds:D2}";

                        var trackItem = new Models.TrackItem
                        {
                            Uri = uri,
                            Title = title,
                            Artist = artist,
                            Album = album,
                            CoverUrl = !string.IsNullOrEmpty(coverUrl) ? coverUrl : defaultCoverUrl,
                            DurationText = durText
                        };
                        list.Add(trackItem);
                    }
                }

                // Check next page URL for pagination
                nextUrl = null;
                if (pagingObj.ValueKind == JsonValueKind.Object && pagingObj.TryGetProperty("next", out var nextProp) && nextProp.ValueKind == JsonValueKind.String)
                {
                    nextUrl = nextProp.GetString();
                }
                else if (root.TryGetProperty("next", out var rootNext) && rootNext.ValueKind == JsonValueKind.String)
                {
                    nextUrl = rootNext.GetString();
                }
            }
            Console.WriteLine($"[SpotifyService] Direct playlist fetch succeeded. Parsed {list.Count} tracks.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SpotifyService] Error parsing direct playlist tracks: {ex.Message}");
        }
        return list;
    }

    public async Task<Paging<PlaylistTrack<IPlayableItem>>?> GetPlaylistItemsAsync(string playlistId, int limit = 100)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(playlistId))
            {
                Console.WriteLine("[SpotifyService] GetPlaylistItemsAsync called with empty playlistId");
                return null;
            }

            string cleanId = playlistId.Trim();
            if (cleanId.StartsWith("spotify:playlist:", StringComparison.OrdinalIgnoreCase))
            {
                cleanId = cleanId["spotify:playlist:".Length..];
            }
            else if (cleanId.Contains("/playlist/"))
            {
                cleanId = cleanId[(cleanId.IndexOf("/playlist/") + "/playlist/".Length)..];
            }

            if (cleanId.Contains('?')) cleanId = cleanId.Split('?')[0];
            int colonIdx = cleanId.LastIndexOf(':');
            if (colonIdx >= 0) cleanId = cleanId[(colonIdx + 1)..];

            try
            {
                var request = new PlaylistGetItemsRequest
                {
                    Limit = limit
                };
                var result = await Client.Playlists.GetPlaylistItems(cleanId, request);
                if (result?.Items != null && result.Items.Any(i => i.Track != null))
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SpotifyService] GetPlaylistItems failed for '{cleanId}': {ex.Message}");
            }

            try
            {
                var fullPlaylist = await Client.Playlists.Get(cleanId);
                if (fullPlaylist?.Items != null)
                {
                    return fullPlaylist.Items;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SpotifyService] Get full playlist failed for '{cleanId}': {ex.Message}");
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SpotifyService] Error fetching playlist items for '{playlistId}': {ex.Message}");
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
