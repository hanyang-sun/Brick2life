param(
    [Parameter(Mandatory = $true)][string]$RunDirectory,
    [ValidateSet('completed', 'cancelled')][string]$ExpectedStatus = 'completed'
)
$ErrorActionPreference = 'Stop'
$runPath = [IO.Path]::GetFullPath($RunDirectory)
$manifest = Get-Content -LiteralPath (Join-Path $runPath 'manifest.json') -Raw | ConvertFrom-Json
$request = Get-Content -LiteralPath (Join-Path $runPath 'request.json') -Raw | ConvertFrom-Json
$script:assertions = 0
function Assert-That([bool]$Condition, [string]$Message) {
    $script:assertions++
    if (!$Condition) { throw $Message }
}
function Hash-File([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
Assert-That ($manifest.schemaVersion -eq 1 -and $manifest.generatorVersion -eq 'daily-v1') 'Unknown dataset schema/generator.'
Assert-That ($manifest.status -eq $ExpectedStatus) ('Unexpected status: ' + $manifest.status)
Assert-That ($manifest.plannedImages -eq $request.layoutCount * $request.lightingCount) 'Requested count changed.'
Assert-That ($manifest.samples.Count -eq $manifest.completedImages) 'Manifest sample count differs from completed count.'
Assert-That ($manifest.completedImages -le $manifest.plannedImages) 'Run exceeded requested count.'
Assert-That ($manifest.seed -eq $request.seed) 'Seed changed.'
if ($ExpectedStatus -eq 'completed') {
    Assert-That ($manifest.completedImages -eq $manifest.plannedImages) 'Completed run is incomplete.'
    Assert-That ($manifest.layouts.Count -eq $request.layoutCount) 'Distinct layout count differs from request.'
    Assert-That (@($manifest.layouts.layoutGroupId | Sort-Object -Unique).Count -eq $request.layoutCount) 'Repeated logical layouts.'
}
$framePath = Join-Path $runPath 'frames'
$pngs = @(Get-ChildItem -LiteralPath $framePath -Filter '*.png' -File -ErrorAction SilentlyContinue)
$jsons = @(Get-ChildItem -LiteralPath $framePath -Filter '*.json' -File -ErrorAction SilentlyContinue)
Assert-That ($pngs.Count -eq $manifest.completedImages -and $jsons.Count -eq $manifest.completedImages) 'Orphaned/missing frame files.'
Assert-That (@(Get-ChildItem -LiteralPath $runPath -Recurse -Filter '*.tmp' -File).Count -eq 0) 'Temporary exports were left behind.'
$frameIds = @{}
$cameraIds = @{}
foreach ($sample in $manifest.samples) {
    Assert-That (!$frameIds.ContainsKey($sample.frameId)) 'Duplicate frame ID.'
    $frameIds[$sample.frameId] = $true
    $cameraIds[$sample.cameraConfigurationId] = $true
    Assert-That ($sample.sampleIndex -eq $frameIds.Count - 1) 'Sample sequence is not contiguous.'
    $pngPath = Join-Path $runPath $sample.image
    $jsonPath = Join-Path $runPath $sample.groundTruth
    Assert-That ((Hash-File $pngPath) -eq $sample.imageSha256 -and (Hash-File $jsonPath) -eq $sample.groundTruthSha256) 'Sample content hash mismatch.'
    $truth = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
    Assert-That ($truth.schemaVersion -eq 3 -and $truth.cameraConfiguration.schemaVersion -eq 1 -and $truth.lightingConfiguration.schemaVersion -eq 2) 'Existing capture schemas changed.'
    Assert-That ($truth.imageFile -eq [IO.Path]::GetFileName($pngPath) -and $truth.frameId -eq $sample.frameId) 'PNG and GT association is incorrect.'
    Assert-That ($truth.cells.Count -eq 100 -and $truth.gridWidth -eq 10 -and $truth.gridHeight -eq 10) 'Incorrect board dimensions.'
    Assert-That ($truth.layoutVersion -eq $sample.layoutVersion -and $truth.cameraConfigurationId -eq $sample.cameraConfigurationId -and $truth.lightingConfigurationId -eq $sample.lightingConfigurationId) 'Capture/manifest identity mismatch.'
    $bytes = [IO.File]::ReadAllBytes($pngPath)
    Assert-That ([BitConverter]::ToString($bytes, 0, 8) -eq '89-50-4E-47-0D-0A-1A-0A') 'Invalid PNG header.'
    $width = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes, 16))
    $height = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes, 20))
    Assert-That ($width -eq $sample.width -and $height -eq $sample.height -and $width -eq $truth.imageWidth -and $height -eq $truth.imageHeight) 'PNG dimensions differ from GT.'
    $canonical = [Text.StringBuilder]::new('board-content-v1;')
    $occupied = 0
    for ($i = 0; $i -lt 100; $i++) {
        $cell = $truth.cells[$i]
        Assert-That ($cell.gridX -eq $i % 10 -and $cell.gridY -eq [int][Math]::Floor($i / 10) -and $cell.layer -eq 0) 'Cell coordinates/order are incorrect.'
        if ($cell.occupied) {
            $occupied++
            Assert-That ($manifest.palette -ccontains $cell.colorId -and ![string]::IsNullOrWhiteSpace($cell.brickId)) 'Invalid occupied cell.'
            [void]$canonical.Append('2:').Append($cell.colorId.Length).Append(':').Append($cell.colorId).Append(';')
        } else {
            Assert-That (!$cell.colorId -and !$cell.brickId) 'Empty cell carries occupied metadata.'
            [void]$canonical.Append('1:0:;')
        }
    }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $groupId = 'layout-' + [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical.ToString()))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
    Assert-That ($groupId -eq $sample.layoutGroupId -and $occupied -eq $sample.occupiedCells) 'RGB/GT layout group differs from the plan.'
    $layout = $manifest.layouts[$sample.layoutIndex]
    Assert-That ($layout.layoutGroupId -eq $groupId -and $layout.family -eq $sample.family) 'Sample belongs to the wrong layout.'
}
Assert-That ($cameraIds.Count -le 1) 'Fixed-camera run unexpectedly changed camera geometry.'
foreach ($group in ($manifest.samples | Group-Object layoutGroupId)) {
    if ($ExpectedStatus -eq 'completed') {
        Assert-That ($group.Count -eq $request.lightingCount) 'Layout is missing lighting conditions.'
        Assert-That (@($group.Group.lightingConfigurationId | Sort-Object -Unique).Count -eq $request.lightingCount) 'Lighting conditions share an ID.'
        Assert-That (@($group.Group.imageSha256 | Sort-Object -Unique).Count -eq $request.lightingCount) 'Lighting variants did not change rendered RGB.'
    }
}
[ordered]@{ passed = $true; assertions = $assertions; status = $manifest.status; images = $manifest.completedImages; layouts = $manifest.layouts.Count; families = @($manifest.layouts.family | Sort-Object -Unique) } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $runPath 'verification.json') -Encoding utf8
Write-Output ("DATASET_CHECKS_PASSED: $assertions assertions; " + $manifest.completedImages + ' paired images; status=' + $manifest.status)
