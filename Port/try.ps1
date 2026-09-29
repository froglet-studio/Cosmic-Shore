# Try the Unity-less build from ANY branch of an existing Cosmic Shore clone, without
# switching branches. Run from inside the clone (paste into PowerShell):
#
#   git fetch origin cece/focused-planck-cj46y3; & ([scriptblock]::Create((git show FETCH_HEAD:Port/try.ps1) -join "`n"))
#
# What it does: fetches the port branch, checks it out into a sibling worktree
# (..\CosmicShore-unityless, created once, updated in place after that), unpacks the
# self-contained Windows build there if it changed, and launches it. Your own checkout and
# branch are never touched. The build reads scenes/art/audio from that worktree's Assets/.

$ErrorActionPreference = 'Continue'
$branch = 'cece/focused-planck-cj46y3'

$root = (git rev-parse --show-toplevel 2>$null)
if (-not $root) { Write-Host 'Run this from inside your Cosmic Shore clone.'; return }
$root = $root.Trim()
$wt = Join-Path (Split-Path $root -Parent) 'CosmicShore-unityless'

Write-Host "Fetching $branch ..."
git -C $root fetch origin $branch
if ($LASTEXITCODE -ne 0) { Write-Host 'git fetch failed - check your connection/auth.'; return }
$target = (git -C $root rev-parse FETCH_HEAD).Trim()

if (-not (Test-Path (Join-Path $wt '.git'))) {
    Write-Host "Creating worktree at $wt (first run only) ..."
    git -C $root worktree add --detach $wt $target
} else {
    git -C $wt checkout --detach --force $target
}
if ($LASTEXITCODE -ne 0) { Write-Host 'git worktree update failed.'; return }

$zip = Join-Path $wt 'Port\dist\CosmicShore-Player-Windows.zip'
$out = Join-Path $wt 'Port\dist\Player-latest'
$stamp = Join-Path $out '.build-commit'
$zipCommit = (git -C $wt log -1 --format=%H -- 'Port/dist/CosmicShore-Player-Windows.zip').Trim()
if (-not (Test-Path $stamp) -or (Get-Content $stamp -Raw).Trim() -ne $zipCommit) {
    Write-Host 'Unpacking the latest build ...'
    if (Test-Path $out) { Remove-Item -Recurse -Force $out }
    Expand-Archive -Force $zip $out
    Set-Content -Path $stamp -Value $zipCommit
}

Write-Host "Launching (build $($zipCommit.Substring(0,9))) ..."
Start-Process -FilePath (Join-Path $out 'CosmicShore.exe') -WorkingDirectory $out
