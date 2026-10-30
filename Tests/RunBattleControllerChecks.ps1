param(
    [string]$UnityPath = 'D:\Unity\Editor\6000.3.24f1\Editor\Unity.exe',
    [string]$ValidationProject,
    [ValidateSet('BattleControllerChecks', 'BattleLiveEditingChecks')]
    [string]$CheckClass = 'BattleControllerChecks'
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) { throw "Unity editor not found: $UnityPath" }
if ([string]::IsNullOrWhiteSpace($ValidationProject)) {
    $ValidationProject = Join-Path $repoRoot ('.scratch\battle-controller\run-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
}
$runPath = [IO.Path]::GetFullPath($ValidationProject)
$validationRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot '.scratch\battle-controller')) + [IO.Path]::DirectorySeparatorChar
if (-not $runPath.StartsWith($validationRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'ValidationProject must be inside .scratch/battle-controller, outside the interactive project assets.'
}
New-Item -ItemType Directory -Path (Join-Path $runPath 'Assets\Editor'), (Join-Path $runPath 'Packages'), (Join-Path $runPath 'ProjectSettings') -Force | Out-Null
foreach ($folder in @('Core', 'Application', 'Vision', 'Battle', 'Simulation', 'Feedback')) {
    $destination = Join-Path $runPath ('Assets\Scripts\' + $folder)
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item -Path (Join-Path $repoRoot ('Assets\Scripts\' + $folder + '\*')) -Destination $destination -Recurse -Force
}
foreach ($file in @('ProjectVersion.txt', 'ProjectSettings.asset')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot ('ProjectSettings\' + $file)) -Destination (Join-Path $runPath ('ProjectSettings\' + $file)) -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Unity\BattleControllerChecks.cs') -Destination (Join-Path $runPath 'Assets\Editor\BattleControllerChecks.cs') -Force
$editingChecks = Join-Path $PSScriptRoot 'Unity\BattleLiveEditingChecks.cs'
if (Test-Path -LiteralPath $editingChecks) {
    Copy-Item -LiteralPath $editingChecks -Destination (Join-Path $runPath 'Assets\Editor\BattleLiveEditingChecks.cs') -Force
}
foreach ($oldName in @('BattleEditingLockChecks.cs','BattleEditingLockChecks.cs.meta')) {
    $oldCheck = Join-Path $runPath ('Assets\Editor\' + $oldName)
    if (Test-Path -LiteralPath $oldCheck) { Remove-Item -LiteralPath $oldCheck }
}
$manifest = Get-Content -LiteralPath (Join-Path $repoRoot 'Packages\manifest.json') -Raw | ConvertFrom-Json
$manifest.dependencies.PSObject.Properties.Remove('com.unity.multiplayer.center')
[IO.File]::WriteAllText((Join-Path $runPath 'Packages\manifest.json'), ($manifest | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
$resultPath = Join-Path $runPath 'verification.json'
if (Test-Path -LiteralPath $resultPath) {
    Move-Item -LiteralPath $resultPath -Destination (Join-Path $runPath ('verification-previous-' + [guid]::NewGuid().ToString('N') + '.json'))
}
$arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $runPath + '"'), '-executeMethod', ($CheckClass + '.Run'), '-logFile', ('"' + (Join-Path $runPath 'editor.log') + '"'))
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output ('Validation project: ' + $runPath)
$process.WaitForExit()
if (Test-Path -LiteralPath $resultPath) {
    Copy-Item -LiteralPath $resultPath -Destination (Join-Path $runPath ('verification-' + $CheckClass + '.json')) -Force
    Get-Content -LiteralPath $resultPath -Raw
}
else { Write-Output 'Unity did not produce verification.json; inspect editor.log in the validation project.' }
if ($process.ExitCode -ne 0) { exit $process.ExitCode }
if (-not (Test-Path -LiteralPath $resultPath)) { exit 1 }
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if (-not $result.passed) { exit 1 }
exit 0
