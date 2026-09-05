$token = (Get-Content "$env:APPDATA\IrreSpotify\tokens.json" | ConvertFrom-Json).AccessToken
$headers = @{ "Authorization" = "Bearer $token" }

$id = "5GdX9V6FVGNIy810isx2Ol"

$urls = @(
    "https://api.spotify.com/v1/playlists/${id}?market=ES",
    "https://api.spotify.com/v1/playlists/${id}?fields=name,tracks",
    "https://api.spotify.com/v1/playlists/${id}?fields=tracks",
    "https://api.spotify.com/v1/users/me/playlists"
)

foreach ($url in $urls) {
    Write-Host "Testing URL: $url"
    try {
        $res = Invoke-RestMethod -Uri $url -Headers $headers -Method Get
        Write-Host "SUCCESS! Response keys: $($res.PSObject.Properties.Name -join ', ')"
        if ($res.tracks) { Write-Host "Tracks total: $($res.tracks.total), items: $($res.tracks.items.Count)" }
        if ($res.items) { Write-Host "User Playlists items: $($res.items.Count)" }
    } catch {
        Write-Host "FAILED: $($_.Exception.Message)"
    }
}
