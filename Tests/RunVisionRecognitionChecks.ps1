param([string]$UnityPath = 'D:\Unity\Editor\6000.3.24f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$runPath = Join-Path $repoRoot ('.scratch\vision-recognition\run-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path (Join-Path $runPath 'Assets\Editor'), (Join-Path $runPath 'Packages'), (Join-Path $runPath 'ProjectSettings') -Force | Out-Null
foreach ($folder in @('Scripts\Core', 'Scripts\Application', 'Scripts\Vision', 'Scripts\Simulation', 'Scenes', 'Materials', 'RenderTextures', 'Resources')) {
    $destination = Join-Path $runPath ('Assets\' + $folder)
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot ('Assets\' + $folder)) -Destination $destination -Recurse
}
foreach ($file in @('ProjectVersion.txt', 'QualitySettings.asset', 'GraphicsSettings.asset', 'ProjectSettings.asset')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot ('ProjectSettings\' + $file)) -Destination (Join-Path $runPath ('ProjectSettings\' + $file))
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Unity\VisionRecognitionChecks.cs') -Destination (Join-Path $runPath 'Assets\Editor\VisionRecognitionChecks.cs')
$manifest = Get-Content -LiteralPath (Join-Path $repoRoot 'Packages\manifest.json') -Raw | ConvertFrom-Json
$manifest.dependencies.PSObject.Properties.Remove('com.unity.multiplayer.center')
$utf8NoBom = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $runPath 'Packages\manifest.json'), ($manifest | ConvertTo-Json -Depth 8), $utf8NoBom)
$arguments = @('-batchmode', '-projectPath', ('"' + $runPath + '"'), '-executeMethod', 'VisionRecognitionChecks.Run', '-logFile', ('"' + (Join-Path $runPath 'editor.log') + '"'))
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output ('Validation project: ' + $runPath)
$process.WaitForExit()
$resultPath = Join-Path $runPath 'verification.json'
if (Test-Path -LiteralPath $resultPath) { Get-Content -LiteralPath $resultPath -Raw }
else { Write-Output 'Unity did not produce verification.json; inspect editor.log in the validation project.' }
exit $process.ExitCode
