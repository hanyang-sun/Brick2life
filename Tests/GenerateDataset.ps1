param(
    [string]$UnityPath = 'D:\Unity\Editor\6000.3.24f1\Editor\Unity.exe',
    [ValidateRange(1, 200)][int]$LayoutCount = 20,
    [int]$Seed = 20261020,
    [ValidateRange(1, 1023)][int]$Families = 1023,
    [ValidateSet(1, 5)][int]$LightingCount = 5,
    [string]$OutputRoot,
    [string]$RunDirectory,
    [switch]$NoWait
)
$ErrorActionPreference = 'Stop'
$utf8NoBom = [Text.UTF8Encoding]::new($false)
$repoRoot = Split-Path -Parent $PSScriptRoot
if (!(Test-Path -LiteralPath $UnityPath -PathType Leaf)) { throw "Unity editor not found: $UnityPath" }
if ($Families -eq 1 -and $LayoutCount -ne 1) { throw 'Only one distinct empty board exists. Use -LayoutCount 1 or select more families.' }
if (!$OutputRoot) { $OutputRoot = Join-Path $repoRoot 'SimulationCaptures\Datasets' }
$runId = 'dataset-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
if (!$RunDirectory) { $RunDirectory = Join-Path $OutputRoot $runId }
$runDirectoryPath = [IO.Path]::GetFullPath($RunDirectory)
$workDirectoryPath = [IO.Path]::GetFullPath((Join-Path $repoRoot ('.scratch\dataset-generation\' + $runId)))
if (Test-Path -LiteralPath $runDirectoryPath) { throw "Run directory already exists; choose a new directory: $runDirectoryPath" }
New-Item -ItemType Directory -Path $runDirectoryPath | Out-Null
$launcherPath = Join-Path $runDirectoryPath 'launcher.json'
$launcher = [ordered]@{ status = 'preparing'; processId = 0; exitCode = 0; workDirectory = $workDirectoryPath; logPath = (Join-Path $workDirectoryPath 'editor.log'); error = '' }
function Save-Launcher {
    $temporary = $launcherPath + '.tmp'
    [IO.File]::WriteAllText($temporary, ($launcher | ConvertTo-Json -Depth 5), $utf8NoBom)
    if (Test-Path -LiteralPath $launcherPath) { [IO.File]::Replace($temporary, $launcherPath, [NullString]::Value) }
    else { [IO.File]::Move($temporary, $launcherPath) }
}
Save-Launcher
try {
    New-Item -ItemType Directory -Path (Join-Path $workDirectoryPath 'Assets\Editor'), (Join-Path $workDirectoryPath 'Packages'), (Join-Path $workDirectoryPath 'ProjectSettings') -Force | Out-Null
    foreach ($folder in @('Scripts\Core', 'Scripts\Application', 'Scripts\Simulation', 'Scenes', 'Materials', 'RenderTextures', 'Resources', 'Editor')) {
        $destination = Join-Path $workDirectoryPath ('Assets\' + $folder)
        if ($folder -eq 'Editor') {
            Get-ChildItem -LiteralPath (Join-Path $repoRoot 'Assets\Editor') -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $destination }
        } else {
            New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
            Copy-Item -LiteralPath (Join-Path $repoRoot ('Assets\' + $folder)) -Destination $destination -Recurse
        }
    }
    foreach ($file in @('ProjectVersion.txt', 'QualitySettings.asset', 'GraphicsSettings.asset', 'ProjectSettings.asset')) {
        Copy-Item -LiteralPath (Join-Path $repoRoot ('ProjectSettings\' + $file)) -Destination (Join-Path $workDirectoryPath ('ProjectSettings\' + $file))
    }
    $packages = Get-Content -LiteralPath (Join-Path $repoRoot 'Packages\manifest.json') -Raw | ConvertFrom-Json
    $packages.dependencies.PSObject.Properties.Remove('com.unity.multiplayer.center')
    [IO.File]::WriteAllText((Join-Path $workDirectoryPath 'Packages\manifest.json'), ($packages | ConvertTo-Json -Depth 8), $utf8NoBom)
    # Resolve the same dependency versions as the source project.
    $packageLock = Join-Path $repoRoot 'Packages\packages-lock.json'
    if (Test-Path -LiteralPath $packageLock) { Copy-Item -LiteralPath $packageLock -Destination (Join-Path $workDirectoryPath 'Packages\packages-lock.json') }
    $snapshot = @()
    foreach ($root in @('Assets', 'Packages', 'ProjectSettings')) {
        Get-ChildItem -LiteralPath (Join-Path $workDirectoryPath $root) -Recurse -File | Sort-Object FullName | ForEach-Object {
            $snapshot += [ordered]@{ file = $_.FullName.Substring($workDirectoryPath.Length + 1).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
        }
    }
    $sourceJson = [ordered]@{ schemaVersion = 1; sourceProject = $repoRoot; files = $snapshot } | ConvertTo-Json -Depth 6
    [IO.File]::WriteAllText((Join-Path $runDirectoryPath 'source-snapshot.json'), $sourceJson, $utf8NoBom)
    $requestPath = Join-Path $runDirectoryPath 'request.json'
    $requestJson = [ordered]@{ schemaVersion = 1; layoutCount = $LayoutCount; seed = $Seed; families = $Families; lightingCount = $LightingCount; outputDirectory = $runDirectoryPath; workDirectory = $workDirectoryPath } | ConvertTo-Json
    [IO.File]::WriteAllText($requestPath, $requestJson, $utf8NoBom)
    [IO.File]::WriteAllText((Join-Path $workDirectoryPath '.brick2life-dataset-workspace'), $requestPath, $utf8NoBom)
    $arguments = @('-batchmode', '-projectPath', ('"' + $workDirectoryPath + '"'), '-executeMethod', 'Brick2Life.Editor.DatasetGenerationRunner.Run', '-datasetRequest', ('"' + $requestPath + '"'), '-logFile', ('"' + $launcher.logPath + '"'))
    $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $launcher.processId = $process.Id; $launcher.status = 'started'; Save-Launcher
    Write-Output ('Dataset: ' + $runDirectoryPath)
    Write-Output ('Images planned: ' + ($LayoutCount * $LightingCount))
    if ($NoWait) { return }
    $process.WaitForExit()
    $launcher.exitCode = $process.ExitCode; $launcher.status = 'exited'; Save-Launcher
    $manifestPath = Join-Path $runDirectoryPath 'manifest.json'
    if (!(Test-Path -LiteralPath $manifestPath)) { throw ('Unity did not produce a manifest. Inspect ' + $launcher.logPath) }
    $result = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    Write-Output ('Result: ' + $result.status + ' (' + $result.completedImages + '/' + $result.plannedImages + ')')
    if ($process.ExitCode -ne 0 -or $result.status -eq 'failed') { throw ('Dataset generation failed. ' + $result.error) }
    if ($result.status -ne 'completed' -and $result.status -ne 'cancelled') { throw ('Unity exited with an unfinished run: ' + $result.status) }
} catch {
    $launcher.status = 'failed'; $launcher.error = $_.Exception.Message
    if ($process -and !$process.HasExited) { [IO.File]::WriteAllText((Join-Path $runDirectoryPath 'cancel.request'), 'Launcher failed; cancel background capture.') }
    Save-Launcher
    throw
}
