param([string]$UnityPath = 'D:\Unity\Editor\6000.3.24f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$output = Join-Path $repoRoot 'Builds'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$log = Join-Path $output ('build-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.log')
$arguments = @('-batchmode', '-quit', '-projectPath', ('"' + $repoRoot + '"'), '-executeMethod', 'Brick2Life.Editor.Brick2LifeBuild.VerifyAndBuild', '-logFile', ('"' + $log + '"'))
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output ('Build log: ' + $log)
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw ('Unity build failed; inspect ' + $log) }
Get-Content -LiteralPath (Join-Path $output 'architecture-verification.json') -Raw
