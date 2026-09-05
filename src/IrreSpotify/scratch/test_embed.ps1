$id = "3kxMGTfqItYJRTea4DMvG0"
$resp = Invoke-WebRequest -Uri "https://open.spotify.com/embed/playlist/$id" -UserAgent "Mozilla/5.0 (Windows NT 10.0; Win64; x64)" -UseBasicParsing
$html = $resp.Content
if ($html -match '<script id="__NEXT_DATA__" type="application/json">(.*?)</script>') {
    $jsonStr = $matches[1]
    $data = $jsonStr | ConvertFrom-Json
    $t = $data.props.pageProps.state.data.entity.trackList[0]
    Write-Host "Track 0 structure:"
    $t | ConvertTo-Json -Depth 3
}
