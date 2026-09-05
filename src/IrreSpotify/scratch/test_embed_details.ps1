$id = "3kxMGTfqItYJRTea4DMvG0"
$resp = Invoke-WebRequest -Uri "https://open.spotify.com/embed/playlist/$id" -UserAgent "Mozilla/5.0 (Windows NT 10.0; Win64; x64)" -UseBasicParsing
$html = $resp.Content
if ($html -match '<script id="__NEXT_DATA__" type="application/json">(.*?)</script>') {
    $jsonStr = $matches[1]
    $data = $jsonStr | ConvertFrom-Json
    Write-Host "=== Keys in pageProps ==="
    Write-Host ($data.props.pageProps.PSObject.Properties.Name -join ', ')

    Write-Host "`n=== Keys in state ==="
    Write-Host ($data.props.pageProps.state.PSObject.Properties.Name -join ', ')

    Write-Host "`n=== Keys in state.data ==="
    Write-Host ($data.props.pageProps.state.data.PSObject.Properties.Name -join ', ')

    if ($data.props.pageProps.state.data.resources) {
        Write-Host "`n=== Resources keys ==="
        Write-Host ($data.props.pageProps.state.data.resources.PSObject.Properties.Name -join ', ')
    }
}
