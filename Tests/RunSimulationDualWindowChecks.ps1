param([string]$UnityPath = 'D:\Unity\Editor\6000.3.24f1\Editor\Unity.exe', [string]$ValidationProject)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ValidationProject)) {
    $ValidationProject = Join-Path $repoRoot ('.scratch\dual-window\run-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
}
$runPath = [IO.Path]::GetFullPath($ValidationProject)
$allowed = [IO.Path]::GetFullPath((Join-Path $repoRoot '.scratch\dual-window')) + [IO.Path]::DirectorySeparatorChar
if (-not $runPath.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'ValidationProject must be inside .scratch/dual-window.' }
New-Item -ItemType Directory -Path (Join-Path $runPath 'Assets\Editor'), (Join-Path $runPath 'Packages'), (Join-Path $runPath 'ProjectSettings') -Force | Out-Null
foreach ($folder in @('Scripts\Core','Scripts\Application','Scripts\Vision','Scripts\Battle','Scripts\Simulation','Scripts\Feedback','Scenes','Materials','RenderTextures','Resources')) {
    $destination = Join-Path $runPath ('Assets\' + $folder)
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item -Path (Join-Path $repoRoot ('Assets\' + $folder + '\*')) -Destination $destination -Recurse -Force
}
foreach ($file in @('SimulationProjectionPreview.cs','SimulationProjectionWindow.cs','SimulationDualWindowSession.cs')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot ('Assets\Editor\' + $file)) -Destination (Join-Path $runPath ('Assets\Editor\' + $file)) -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Unity\SimulationDualWindowChecks.cs') -Destination (Join-Path $runPath 'Assets\Editor\SimulationDualWindowChecks.cs') -Force
foreach ($file in @('ProjectVersion.txt','ProjectSettings.asset','QualitySettings.asset','GraphicsSettings.asset','TagManager.asset','InputManager.asset')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot ('ProjectSettings\' + $file)) -Destination (Join-Path $runPath ('ProjectSettings\' + $file)) -Force
}
$manifest = Get-Content -LiteralPath (Join-Path $repoRoot 'Packages\manifest.json') -Raw | ConvertFrom-Json
$manifest.dependencies.PSObject.Properties.Remove('com.unity.multiplayer.center')
[IO.File]::WriteAllText((Join-Path $runPath 'Packages\manifest.json'), ($manifest | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
$resultPath = Join-Path $runPath 'verification.json'
if (Test-Path -LiteralPath $resultPath) { Move-Item -LiteralPath $resultPath -Destination (Join-Path $runPath ('verification-previous-' + [guid]::NewGuid().ToString('N') + '.json')) }
$arguments = @('-batchmode','-projectPath',('"' + $runPath + '"'),'-executeMethod','SimulationDualWindowChecks.Run','-logFile',('"' + (Join-Path $runPath 'editor.log') + '"'))
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output ('Validation project: ' + $runPath)
$process.WaitForExit()
if (Test-Path -LiteralPath $resultPath) { Get-Content -LiteralPath $resultPath -Raw } else { Write-Output 'No verification.json; inspect editor.log.'; exit 1 }
if ($process.ExitCode -ne 0) { exit $process.ExitCode }
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if (-not $result.passed) { exit 1 }
exit 0
