<#
.SYNOPSIS
  /androidbuild helper. Builds an Android development APK from the latest origin/<branch>
  in a dedicated build worktree, copies it to a Google Drive for desktop folder, and
  (in -Stage post) posts the share link to the Froglet Games #builds Discord channel.

.DESCRIPTION
  Never touches your working clone: it only runs `git fetch` there. The build happens in a
  separate detached worktree that keeps its Library folder, so repeat builds are incremental.

  Stage "build" (default) prints exactly one machine-readable last line:
    RESULT <shorthash> | <apkFileName> | <sizeMB>MB | <minutes>min | <commit subject>
  or "NO CHANGE <shorthash>" when -SkipIfHash matches, or "FAIL <reason>" and exit 1.

  Stage "post" sends -Link to the Discord webhook in $env:CS_DISCORD_BUILDS_WEBHOOK.

  Every path has an environment-variable override so other machines can use it:
    CS_REPO_DIR         your clone                     (default: the repo this script is in)
    CS_BUILD_WORKTREE   build worktree                 (default: C:\CosmicShoreBuild\worktree)
    CS_BUILD_OUT        Unity output folder            (default: C:\CosmicShoreBuild\out)
    CS_BUILD_DRIVE_DIR  Drive for desktop folder       (default: G:\My Drive\Cosmic Shore Builds)
    CS_UNITY_EXE        Unity.exe                      (default: found from ProjectVersion.txt)
    CS_DISCORD_BUILDS_WEBHOOK  #builds webhook URL     (needed for -Stage post; never commit it)
#>
param(
  [ValidateSet('build','post')] [string]$Stage = 'build',
  [string]$Branch = 'bleeding-edge',
  [string]$SkipIfHash = '',
  [switch]$DiagnosticsOff,
  # post stage
  [string]$Link = '',
  [string]$ApkName = '',
  [string]$Subject = '',
  [string]$SizeMB = ''
)

$ErrorActionPreference = 'Stop'
function Fail($msg) { Write-Output "FAIL $msg"; exit 1 }
function Env-Or($name, $default) { $v = [Environment]::GetEnvironmentVariable($name); if ([string]::IsNullOrWhiteSpace($v)) { $default } else { $v } }

# ---------------------------------------------------------------- post
if ($Stage -eq 'post') {
  $hook = $env:CS_DISCORD_BUILDS_WEBHOOK
  if ([string]::IsNullOrWhiteSpace($hook)) { Fail 'CS_DISCORD_BUILDS_WEBHOOK is not set (see SKILL.md, "Discord").' }
  if ([string]::IsNullOrWhiteSpace($Link)) { Fail '-Link is required for -Stage post.' }
  $who = Env-Or 'USERNAME' 'someone'
  $lines = @("**New Cosmic Shore Android build** ($Branch)")
  if ($ApkName) { $lines += "``$ApkName``" + $(if ($SizeMB) { " ($SizeMB MB)" } else { '' }) }
  if ($Subject) { $lines += "Latest commit: $Subject" }
  $lines += $Link
  $body = @{ content = ($lines -join "`n"); username = 'Cosmic Shore Builds' } | ConvertTo-Json -Compress
  try {
    Invoke-RestMethod -Uri $hook -Method Post -ContentType 'application/json; charset=utf-8' `
      -Body ([Text.Encoding]::UTF8.GetBytes($body)) | Out-Null
  } catch { Fail "Discord webhook post failed: $($_.Exception.Message)" }
  Write-Output 'POSTED'
  exit 0
}

# ---------------------------------------------------------------- build
$repo     = Env-Or 'CS_REPO_DIR' (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$worktree = Env-Or 'CS_BUILD_WORKTREE' 'C:\CosmicShoreBuild\worktree'
$out      = Env-Or 'CS_BUILD_OUT' 'C:\CosmicShoreBuild\out'
$driveDir = Env-Or 'CS_BUILD_DRIVE_DIR' 'G:\My Drive\Cosmic Shore Builds'

if (-not (Test-Path (Join-Path $repo '.git'))) { Fail "CS_REPO_DIR ($repo) is not a git clone." }

# 1. Fetch only. The working clone's checkout and files are never changed.
git -C $repo fetch origin $Branch --prune --quiet
if ($LASTEXITCODE -ne 0) { Fail "git fetch origin $Branch failed." }
$hash  = (git -C $repo rev-parse "origin/$Branch").Trim()
$short = $hash.Substring(0, 9)
$subj  = (git -C $repo log -1 --format=%s $hash).Trim()
if ($SkipIfHash -and $hash.StartsWith($SkipIfHash)) { Write-Output "NO CHANGE $short"; exit 0 }

# 2. Detached build worktree (created on first run; keeps Library between runs).
if (-not (Test-Path (Join-Path $worktree '.git'))) {
  New-Item -ItemType Directory -Force -Path (Split-Path $worktree) | Out-Null
  git -C $repo worktree add --detach $worktree $hash
  if ($LASTEXITCODE -ne 0) { Fail "git worktree add $worktree failed." }
} else {
  # Someone may have left work in the build worktree. Stash it rather than delete it.
  $dirty = git -C $worktree status --porcelain --untracked-files=normal -- . ':!Library' ':!Builds' ':!Temp' ':!Logs' ':!obj'
  if ($dirty) {
    git -C $worktree stash push -u -m "androidbuild auto-stash before $short" | Out-Null
    Write-Output "NOTE stashed uncommitted changes in $worktree (git -C `"$worktree`" stash list)"
  }
  git -C $worktree checkout --detach --force $hash --quiet
  if ($LASTEXITCODE -ne 0) { Fail "checkout $short in $worktree failed." }
}

# 3. Unity version from the project, then find the editor.
$verLine = Select-String -Path (Join-Path $worktree 'ProjectSettings\ProjectVersion.txt') -Pattern '^m_EditorVersion:\s*(\S+)' | Select-Object -First 1
if (-not $verLine) { Fail 'Could not read ProjectSettings\ProjectVersion.txt.' }
$ver = $verLine.Matches[0].Groups[1].Value
$unity = $env:CS_UNITY_EXE
if ([string]::IsNullOrWhiteSpace($unity)) {
  $candidates = @(
    "C:\Unity\$ver\Editor\Unity.exe",
    "C:\Program Files\Unity\Hub\Editor\$ver\Editor\Unity.exe",
    "D:\Unity\$ver\Editor\Unity.exe"
  )
  $unity = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $unity -or -not (Test-Path $unity)) { Fail "Unity $ver not found. Install it with Android Build Support, or set CS_UNITY_EXE." }

# Unity refuses a project that is already open; check before spending the time.
if (Test-Path (Join-Path $worktree 'Temp\UnityLockfile')) {
  $open = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -like "*$worktree*" }
  if ($open) { Fail "Unity already has $worktree open (PID $($open[0].ProcessId)). Close it or wait for that build." }
}

# 4. Batchmode build, blocking on the Unity process.
New-Item -ItemType Directory -Force -Path $out | Out-Null
$log = Join-Path $out 'build.log'
$started = Get-Date
$unityArgs = @('-quit','-batchmode','-nographics','-buildTarget','Android','-projectPath',$worktree,
          '-executeMethod','CosmicShore.Editor.CosmicShoreBuildPipeline.BuildAndroidDevelopment',
          '-buildOutput',$out,'-logFile',$log)
if ($DiagnosticsOff) { $unityArgs += '-diagnosticsOff' }
Write-Output "BUILDING $short ($subj) with Unity $ver"
# Unity.exe is a GUI-subsystem program; piping to Out-Null makes PowerShell wait for it and
# quotes paths with spaces correctly (Start-Process -ArgumentList does not).
& $unity @unityArgs | Out-Null
$exit = $LASTEXITCODE
$mins = [math]::Round(((Get-Date) - $started).TotalMinutes)
if ($exit -ne 0) {
  Write-Output '--- [Build] / error lines ---'
  Select-String -Path $log -Pattern '\[Build\]|error CS|BuildFailedException|Error building' | Select-Object -Last 25 | ForEach-Object { $_.Line }
  Write-Output '--- last 40 log lines ---'
  Get-Content $log -Tail 40
  Fail "Unity exited $exit after $mins min. Full log: $log"
}

# 5. Rename the newest dev APK to CosmicShore-<branch>-<short>-dev.apk.
$apk = Get-ChildItem $out -Filter '*-dev.apk' | Where-Object { $_.LastWriteTime -ge $started } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $apk) { Fail "Unity exited 0 but no new *-dev.apk is in $out." }
$name = "CosmicShore-$($Branch -replace '[\\/]','-')-$short-dev.apk"
$final = Join-Path $out $name
if ($apk.FullName -ne $final) { Move-Item -Force $apk.FullName $final }
$sizeMB = [math]::Round((Get-Item $final).Length / 1MB)

# 6. Copy to the Google Drive for desktop folder (restart Drive if the drive letter is gone).
$driveRoot = Split-Path -Qualifier $driveDir
if ($driveRoot -and -not (Test-Path $driveRoot)) {
  $gd = Get-ChildItem 'C:\Program Files\Google\Drive File Stream' -Recurse -Filter 'GoogleDriveFS.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($gd) { Start-Process $gd.FullName; for ($i = 0; $i -lt 30 -and -not (Test-Path $driveRoot); $i++) { Start-Sleep 2 } }
}
if (-not (Test-Path $driveRoot)) { Fail "Google Drive for desktop is not mounted at $driveRoot. Start it, or set CS_BUILD_DRIVE_DIR. APK is at $final" }
New-Item -ItemType Directory -Force -Path $driveDir | Out-Null
Copy-Item -Force $final (Join-Path $driveDir $name)
Copy-Item -Force $log (Join-Path $driveDir "$($name -replace '\.apk$','')-build.log")

Write-Output "RESULT $short | $name | ${sizeMB}MB | ${mins}min | $subj"
