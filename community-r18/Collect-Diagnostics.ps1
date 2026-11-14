param([string[]]$GameDirectory, [string]$Destination)
$ErrorActionPreference = 'Stop'

try {
    if (-not $GameDirectory -and (Test-Path (Join-Path $PSScriptRoot 'Prime.exe'))) {
        $GameDirectory = @($PSScriptRoot)
    }
    $usualGame = 'C:\Games\Star Trek Fleet Command\Star Trek Fleet Command\default\game'
    if (-not $GameDirectory -and (Test-Path -LiteralPath (Join-Path $usualGame 'Prime.exe') -PathType Leaf)) {
        $GameDirectory = @($usualGame)
    }
    if (-not $GameDirectory) {
        Add-Type -AssemblyName System.Windows.Forms
        $picker = New-Object Windows.Forms.FolderBrowserDialog
        $picker.Description = 'Select the STFC game folder containing Prime.exe. The game can be closed or frozen.'
        $picker.ShowNewFolderButton = $false
        try {
            if ($picker.ShowDialog() -ne [Windows.Forms.DialogResult]::OK) { throw 'Collection cancelled.' }
            $GameDirectory = @($picker.SelectedPath)
        } finally { $picker.Dispose() }
    }
    foreach ($directory in $GameDirectory) {
        if (-not (Test-Path -LiteralPath (Join-Path $directory 'Prime.exe') -PathType Leaf)) {
            throw ('The selected folder does not contain Prime.exe: ' + $directory)
        }
    }
    if (-not $Destination) { $Destination = [Environment]::GetFolderPath('Desktop') }
    if (-not $Destination) { $Destination = $PSScriptRoot }
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    $label = 'STFC-Community-Diagnostics-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')
    $folder = Join-Path $Destination $label
    New-Item -ItemType Directory -Path $folder | Out-Null
    $notes = New-Object System.Collections.Generic.List[string]
    $count = 0
    $gameIndex = 0
    foreach ($game in ($GameDirectory | Select-Object -Unique)) {
        $game = (Resolve-Path -LiteralPath $game).Path.TrimEnd('\')
        $gameIndex++
        $notes.Add("Game $gameIndex : $game")
        $dll = Join-Path $game 'version.dll'
        if (Test-Path -LiteralPath $dll) {
            $identity = [Diagnostics.FileVersionInfo]::GetVersionInfo($dll)
            $notes.Add('DLL description: ' + $identity.FileDescription)
            $notes.Add('DLL version: ' + $identity.FileVersion)
            $notes.Add('DLL identity: ' + $identity.Comments)
            $notes.Add('DLL SHA256: ' + (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash)
        } else { $notes.Add('version.dll was not found beside Prime.exe.') }
        $patterns = @('*_hostile_action.log', '*_collective_diagnostic.log', '*_collective_crash_*.dmp',
                      'community_patch*.log', 'Run17OccupiedMinerDiag.log', '*_fleet_diagnostics.log', '*.log.1', '*.log.2', '*.log.3')
        $roots = @($game)
        $modRoot = Join-Path $game 'stfc-mod'
        if (Test-Path -LiteralPath $modRoot) { $roots += $modRoot }
        $files = @()
        foreach ($root in $roots) {
            foreach ($pattern in $patterns) {
                $files += @(Get-ChildItem -LiteralPath $root -Filter $pattern -File -Recurse:($root -eq $modRoot) -ErrorAction SilentlyContinue)
            }
        }
        foreach ($file in ($files | Sort-Object FullName -Unique)) {
            $relative = $file.FullName.Substring($game.Length).TrimStart('\')
            $target = Join-Path (Join-Path $folder "Game-$gameIndex") $relative
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
            try { Copy-Item -LiteralPath $file.FullName -Destination $target; $count++ }
            catch { $notes.Add('Could not copy ' + $file.FullName + ': ' + $_.Exception.Message) }
        }
    }
    $notes.Add("Collected $count log/dump files. Configurations and exclusions were not modified.")
    $notes | Set-Content -LiteralPath (Join-Path $folder 'COLLECTION-INFO.txt') -Encoding UTF8
    if ($count -eq 0) { throw "No matching logs were found. Details are in $folder" }
    $archive = $folder + '.zip'
    Compress-Archive -Path (Join-Path $folder '*') -DestinationPath $archive
    Write-Host "Ready to send: $archive"
    exit 0
} catch {
    Write-Host ('Collection failed: ' + $_.Exception.Message)
    exit 1
}

