param([string]$UnityPath = 'D:\Unity\Editor\6000.3.24f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$runPath = Join-Path $repoRoot ('.scratch\camera-metadata-validation\run-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path (Join-Path $runPath 'Assets\Editor'), (Join-Path $runPath 'Packages'), (Join-Path $runPath 'ProjectSettings') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'Assets\Scripts\Simulation') -Destination (Join-Path $runPath 'Assets\Simulation') -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Unity\SimulationCameraMetadataChecks.cs') -Destination (Join-Path $runPath 'Assets\Editor\SimulationCameraMetadataChecks.cs')
Copy-Item -LiteralPath (Join-Path $repoRoot 'ProjectSettings\ProjectVersion.txt') -Destination (Join-Path $runPath 'ProjectSettings\ProjectVersion.txt')
$manifest = Get-Content -LiteralPath (Join-Path $repoRoot 'Packages\manifest.json') -Raw | ConvertFrom-Json
$manifest.dependencies.PSObject.Properties.Remove('com.unity.multiplayer.center')
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $runPath 'Packages\manifest.json') -Encoding utf8
$arguments = @('-batchmode', '-projectPath', ('"' + $runPath + '"'), '-executeMethod', 'SimulationCameraMetadataChecks.Run', '-logFile', ('"' + (Join-Path $runPath 'editor.log') + '"'))
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output ('Validation project: ' + $runPath)
$process.WaitForExit()
$resultPath = Join-Path $runPath 'verification.json'
if (Test-Path -LiteralPath $resultPath) { Get-Content -LiteralPath $resultPath -Raw }
else { Write-Output 'Unity did not produce verification.json; inspect editor.log in the validation project.' }
exit $process.ExitCode
