$token = (Get-Content "$env:APPDATA\IrreSpotify\tokens.json" | ConvertFrom-Json).AccessToken
$headers = @{ "Authorization" = "Bearer $token" }

$trackIds = "2uZWffKoemZDI0gHcBEcDc,5fOjFkFA0k5MTOo1LmnVTO,5KcKUpTEHMfcoAps9d5BvY"
Write-Host "=== Testing GET /v1/tracks?ids=$trackIds ==="
try {
    $res = Invoke-RestMethod -Uri "https://api.spotify.com/v1/tracks?ids=$trackIds" -Headers $headers -Method Get
    Write-Host "SUCCESS! Returned $($res.tracks.Count) tracks"
    foreach ($t in $res.tracks) {
        Write-Host "Track: '$($t.name)' -> Album Cover: $($t.album.images[0].url)"
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)"
}
