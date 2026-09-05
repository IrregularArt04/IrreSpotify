$trackId = "2uZWffKoemZDI0gHcBEcDc"
Write-Host "=== Testing Track oEmbed for $trackId ==="
try {
    $res = Invoke-RestMethod -Uri "https://open.spotify.com/oembed?url=https://open.spotify.com/track/$trackId" -Method Get
    Write-Host "oEmbed Title: $($res.title)"
    Write-Host "oEmbed Thumbnail URL: $($res.thumbnail_url)"
} catch {
    Write-Host "ERROR: $($_.Exception.Message)"
}
