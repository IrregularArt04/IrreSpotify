using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;

namespace IrreSpotify.Services;

public class StoredToken
{
    public string TokenType { get; set; } = "Bearer";
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int ExpiresIn { get; set; } = 3600;

    public bool IsExpired => DateTime.UtcNow >= CreatedAt.AddSeconds(ExpiresIn - 60);
}

public class AppConfig
{
    public string ClientId { get; set; } = string.Empty;
}

public class AuthService
{
    // Client ID de Spotify Developer
    public const string DefaultClientId = "c3a6475e870c4b0daa4aea18b4d73acc";

    private static readonly Uri RedirectUri = new("http://127.0.0.1:5543/callback");
    private readonly string _tokenPath;
    
    public string ClientId { get; set; } = DefaultClientId;
    public string? LastAuthError { get; private set; }
    public PKCETokenResponse? CurrentToken { get; private set; }
    public SpotifyClient? SpotifyClient { get; private set; }
    public event Action<bool>? AuthStateChanged;

    public string? AccessToken => CurrentToken?.AccessToken;
    public bool IsAuthenticated => SpotifyClient != null;

    public AuthService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appFolder = Path.Combine(appData, "IrreSpotify");
        Directory.CreateDirectory(appFolder);
        _tokenPath = Path.Combine(appFolder, "tokens.json");
    }

    public async Task<bool> InitializeAsync()
    {
        if (File.Exists(_tokenPath))
        {
            try
            {
                string json = await File.ReadAllTextAsync(_tokenPath);
                var stored = JsonSerializer.Deserialize<StoredToken>(json);
                if (stored != null && !string.IsNullOrEmpty(stored.RefreshToken))
                {
                    var response = await new OAuthClient().RequestToken(
                        new PKCETokenRefreshRequest(ClientId, stored.RefreshToken)
                    );
                    await SaveTokenAsync(response);
                    CreateSpotifyClient(response.AccessToken);
                    AuthStateChanged?.Invoke(true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Token restoration failed: {ex.Message}");
            }
        }
        AuthStateChanged?.Invoke(false);
        return false;
    }

    public async Task<bool> LoginAsync(string? customClientId = null)
    {
        LastAuthError = null;

        if (!string.IsNullOrWhiteSpace(customClientId))
        {
            ClientId = customClientId.Trim();
        }

        if (string.IsNullOrWhiteSpace(ClientId) || ClientId == "YOUR_CLIENT_ID_HERE")
        {
            Console.WriteLine("Spotify Developer ClientId is missing or default placeholder.");
            LastAuthError = "Client ID is missing.";
            return false;
        }

        var (verifier, challenge) = PKCEUtil.GenerateCodes();

        var server = new EmbedIOAuthServer(RedirectUri, 5543);
        await server.Start();

        var tcs = new TaskCompletionSource<bool>();

        server.AuthorizationCodeReceived += async (s, response) =>
        {
            await server.Stop();
            try
            {
                var initialToken = await new OAuthClient().RequestToken(
                    new PKCETokenRequest(ClientId, response.Code, RedirectUri, verifier)
                );
                await SaveTokenAsync(initialToken);
                CreateSpotifyClient(initialToken.AccessToken);
                AuthStateChanged?.Invoke(true);
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exchanging token: {ex}");
                LastAuthError = ex.Message;
                if (ex is APIException apiEx && apiEx.Response != null)
                {
                    LastAuthError = $"{apiEx.Message} ({apiEx.Response.Body})";
                }
                tcs.SetResult(false);
            }
        };

        server.ErrorReceived += async (s, error, state) =>
        {
            await server.Stop();
            Console.WriteLine($"OAuth Error: {error}");
            LastAuthError = error;
            tcs.SetResult(false);
        };

        var request = new LoginRequest(RedirectUri, ClientId, LoginRequest.ResponseType.Code)
        {
            CodeChallengeMethod = "S256",
            CodeChallenge = challenge,
            Scope = new[]
            {
                Scopes.UserReadPrivate,
                Scopes.UserReadEmail,
                Scopes.UserReadPlaybackState,
                Scopes.UserModifyPlaybackState,
                Scopes.UserReadCurrentlyPlaying,
                Scopes.PlaylistReadPrivate,
                Scopes.PlaylistReadCollaborative,
                Scopes.PlaylistModifyPublic,
                Scopes.PlaylistModifyPrivate,
                Scopes.UserLibraryRead,
                Scopes.UserLibraryModify,
                Scopes.UserTopRead,
                Scopes.Streaming,
                Scopes.AppRemoteControl
            }
        };

        BrowserUtil.Open(request.ToUri());
        return await tcs.Task;
    }

    public void Logout()
    {
        if (File.Exists(_tokenPath))
        {
            try { File.Delete(_tokenPath); } catch { }
        }
        CurrentToken = null;
        SpotifyClient = null;
        AuthStateChanged?.Invoke(false);
    }

    public async Task<bool> EnsureTokenValidAsync()
    {
        if (CurrentToken == null && File.Exists(_tokenPath))
        {
            await InitializeAsync();
        }

        if (CurrentToken != null)
        {
            var storedTime = CurrentToken.CreatedAt;
            if (DateTime.UtcNow >= storedTime.AddSeconds(CurrentToken.ExpiresIn - 120))
            {
                string refreshToken = CurrentToken.RefreshToken;
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    try
                    {
                        Console.WriteLine("[AuthService] Access token near expiration, refreshing...");
                        var response = await new OAuthClient().RequestToken(
                            new PKCETokenRefreshRequest(ClientId, refreshToken)
                        );
                        if (string.IsNullOrEmpty(response.RefreshToken))
                        {
                            response.RefreshToken = refreshToken;
                        }
                        await SaveTokenAsync(response);
                        CreateSpotifyClient(response.AccessToken);
                        Console.WriteLine("[AuthService] Access token refreshed successfully.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AuthService] Automatic token refresh failed: {ex.Message}");
                    }
                }
            }
        }
        return IsAuthenticated;
    }

    private void CreateSpotifyClient(string accessToken)
    {
        var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(new TokenAuthenticator(accessToken, "Bearer"));
        SpotifyClient = new SpotifyClient(config);
    }

    private async Task SaveTokenAsync(PKCETokenResponse token)
    {
        CurrentToken = token;
        var stored = new StoredToken
        {
            TokenType = token.TokenType,
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            ExpiresIn = token.ExpiresIn
        };

        string json = JsonSerializer.Serialize(stored);
        await File.WriteAllTextAsync(_tokenPath, json);
    }
}
