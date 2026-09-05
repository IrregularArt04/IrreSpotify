$trackIds = @(
    "2uZWffKoemZDI0gHcBEcDc", "5fOjFkFA0k5MTOo1LmnVTO", "5KcKUpTEHMfcoAps9d5BvY",
    "00EWeXw1RLUwCMw8pxtXAE", "2VXrBXJ0BlxqljeToATfcr", "0NdxbFFknA7kQ4E2zvJfey"
)

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$tasks = foreach ($id in $trackIds) {
    [System.Threading.Tasks.Task]::Run([Action]{
        try {
            $r = Invoke-RestMethod -Uri "https://open.spotify.com/oembed?url=https://open.spotify.com/track/$id" -TimeoutSec 3
            Write-Host "Track $id -> Cover: $($r.thumbnail_url)"
        } catch {}
    })
}

[System.Threading.Tasks.Task]::WaitAll($tasks)
$sw.Stop()
Write-Host "Parallel fetch of $($trackIds.Count) covers completed in $($sw.ElapsedMilliseconds) ms"
