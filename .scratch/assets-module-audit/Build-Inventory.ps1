$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$assetsRoot = Join-Path $projectRoot 'Assets'

$rules = @{}
function Add-Rule([string]$Path,[string]$Category,[string]$Reason) {
    if ($rules.ContainsKey($Path)) { throw "Duplicate classification: $Path" }
    $rules[$Path] = @{ category=$Category; reason=$Reason }
}

$reuse = @(
 'Scripts/Contracts/CombatModels.cs',
 'Scripts/Domain/LevelPipeline.cs', 'Scripts/Domain/GridPathfinder.cs', 'Scripts/Domain/BattleSimulation.cs',
 'Scripts/Application/BattleSession.cs', 'Scripts/Infrastructure/PhotoPixels.cs',
 'Scripts/Presentation/SoftStrategyTheme.cs', 'Scripts/Presentation/ChineseUi.cs',
 'ThirdParty/AssemblyInfo.cs', 'ThirdParty/Colourful/NormalizedLab.cs', 'ThirdParty/RoyTAStar/MinHeap.cs'
)
foreach ($path in $reuse) { Add-Rule $path '直接复用' '保持现有实现，通过新入口或适配模块调用；适用边界详见报告。' }

$modify = @{
 'Scripts/Contracts/Models.cs'='保留旧游戏合同，增补固定格点、预测状态和事件合同，不让游戏合法性限制感知。'
 'Scripts/Contracts/PhotoEvidence.cs'='增加固定棋盘坐标依据与标定标识，保留相对坐标旧格式。'
 'Scripts/Vision/PhotoModels.cs'='复用 RGB 帧，分离颜色标定与角色映射；增加独立仿真输入配置。'
 'Scripts/Vision/PlainBackgroundRecognizer.cs'='旧实现保留作 Baseline；新增固定网格识别适配，不再以检测边界决定新主线原点。'
 'Scripts/Application/LevelSession.cs'='经预测状态适配接入新来源；保留建造期提交和战斗锁定策略。'
 'Scripts/Infrastructure/ScanReportStore.cs'='在独立实验记录工作合入后增量扩展帧/GT/预测/事件/投影时间关联。'
 'Scripts/Presentation/LevelView.cs'='分离关卡绘制与相机/全局光照设置，统一坐标变换并隔离感知可见层。'
 'Scripts/Presentation/CombatView.cs'='复用显示逻辑，适配统一坐标和渲染层，避免敌人与射程标记进入感知输入。'
 'Editor/DemoSceneSetup.cs'='保留旧场景生成器，修正重置整个构建场景列表的行为。'
}
foreach ($path in $modify.Keys) { Add-Rule $path '需要修改' $modify[$path] }

foreach ($path in @('Editor/OrbitCameraChecks.cs','Editor/PhotoOverlayChecks.cs','Editor/SoftStrategyUiChecks.cs')) {
    Add-Rule $path '可以删除' '旧 UI/手势专用验收工具；仅在归档历史验收、替代覆盖和团队确认后清理，本次不删。'
}

$retain = @(
 'Scripts/Contracts/LayoutReviewModels.cs', 'Scripts/Domain/ExampleScans.cs',
 'Scripts/Application/PhotoScanSession.cs', 'Scripts/Application/LayoutEditSession.cs',
 'Scripts/Infrastructure/CameraCapture.cs', 'Scripts/Infrastructure/PhotoFileSource.cs',
 'Scripts/Infrastructure/PhotoProfileStore.cs', 'Scripts/Infrastructure/LayoutReviewStore.cs',
 'Scripts/Infrastructure/WindowsPhotoFilePicker.cs',
 'Scripts/Presentation/BattlePanel.cs', 'Scripts/Presentation/BoardOrbitCamera.cs',
 'Scripts/Presentation/BoardTouchControls.cs', 'Scripts/Presentation/BoardUiLayout.cs',
 'Scripts/Presentation/BrickDefenseDemo.cs', 'Scripts/Presentation/DemoUI.cs',
 'Scripts/Presentation/LayoutReviewPanel.cs', 'Scripts/Presentation/PhotoScanPanel.cs',
 'Editor/BattleChecks.cs', 'Editor/DemoSceneChecks.cs', 'Editor/LayoutReviewUiChecks.cs',
 'Editor/PhotoFilePicker.cs', 'Editor/PhotoScanTools.cs',
 'Resources/photo-scan-settings.json', 'Scenes/BrickDefenseDemo.unity'
)
foreach ($path in $retain) { Add-Rule $path '暂时保留' '支撑旧 Demo、照片 Baseline、回归或调试；新仿真主线不直接依赖其旧流程。' }

$files = @(Get-ChildItem -LiteralPath $assetsRoot -Recurse -File)
$contents = @($files | Where-Object { $_.Extension -ne '.meta' })
$metaFiles = @($files | Where-Object { $_.Extension -eq '.meta' })
$guids = @{}
foreach ($meta in $metaFiles) {
    $target = $meta.FullName.Substring(0,$meta.FullName.Length-5)
    if (-not (Test-Path -LiteralPath $target)) { throw "Orphan meta: $($meta.FullName)" }
    $metaGuid = ([regex]::Match([System.IO.File]::ReadAllText($meta.FullName),'(?m)^guid:\s*(\w+)')).Groups[1].Value
    if (-not $metaGuid -or $guids.ContainsKey($metaGuid)) { throw "Missing or duplicate GUID: $($meta.FullName)" }
    $guids[$metaGuid]=$meta.FullName
}
$textFiles = @($contents | Where-Object { $_.Extension -in @('.cs','.asmdef','.unity','.json','.mat','.md','.txt') })
$texts = @{}
foreach ($file in $textFiles) { $texts[$file.FullName]=[System.IO.File]::ReadAllText($file.FullName) }
$rows = foreach ($file in $contents) {
    $path = $file.FullName.Substring($assetsRoot.Length+1).Replace('\','/')
    if (-not $rules.ContainsKey($path)) {
        if ($file.Extension -eq '.asmdef') {
            Add-Rule $path '直接复用' '保留现有程序集边界及第三方内部可见性；新增依赖按模块单独扩展。'
        } elseif ($path -eq 'Resources/BrickSurface.mat') {
            Add-Rule $path '直接复用' '保留 Standard 运行时着色器依赖，曾修复 Windows 材质初始化失败。'
        } elseif ($path -like 'Resources/Fonts/*' -or $path -like 'ThirdParty/*/*.txt' -or $path -eq 'ThirdParty/NotoSansSC/SOURCE.md') {
            Add-Rule $path '直接复用' '字体及在用第三方代码的许可证/来源随所属资源保留。'
        } else { throw "Unclassified asset: $path" }
    }
    $rule = $rules[$path]
    $metaPath = $file.FullName+'.meta'
    if (-not (Test-Path -LiteralPath $metaPath)) { throw "Missing meta: $path" }
    $guid = ([regex]::Match([System.IO.File]::ReadAllText($metaPath),'(?m)^guid:\s*(\w+)')).Groups[1].Value
    $guidReferences = @()
    if ($guid) {
        $guidReferences = @($textFiles | Where-Object { $_.FullName -ne $file.FullName -and $texts[$_.FullName].Contains($guid) } |
            ForEach-Object { $_.FullName.Substring($assetsRoot.Length+1).Replace('\','/') })
    }
    $types = @()
    $sourceReferences = @()
    if ($file.Extension -eq '.cs') {
        $types = @([regex]::Matches($texts[$file.FullName],'\b(?:class|struct|enum|interface)\s+([A-Za-z_][A-Za-z_0-9]*)') |
            ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
        foreach ($other in @($textFiles | Where-Object { $_.Extension -eq '.cs' -and $_.FullName -ne $file.FullName })) {
            foreach ($type in $types) {
                if ([regex]::IsMatch($texts[$other.FullName],'\b'+[regex]::Escape($type)+'\b')) {
                    $sourceReferences += $other.FullName.Substring($assetsRoot.Length+1).Replace('\','/')
                    break
                }
            }
        }
    }
    [pscustomobject][ordered]@{
        path='Assets/'+$path; category=$rule.category; reason=$rule.reason; bytes=$file.Length
        sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        meta='Assets/'+$path+'.meta'; guid=$guid; declaredTypes=$types
        guidReferences=$guidReferences; lexicalSourceReferences=$sourceReferences
    }
}
if ($rules.Count -ne $contents.Count) { throw 'Rules include missing files.' }
$summary = [ordered]@{
    inspectedAt='2026-10-15'; root=$assetsRoot; baseline='c3bce83daacb4ac3b09434ce96a748c983d55abf'
    totalFiles=$files.Count; contentFiles=$contents.Count; metaFiles=$metaFiles.Count
    directoryMetaFiles=@($metaFiles | Where-Object { Test-Path -LiteralPath $_.FullName.Substring(0,$_.FullName.Length-5) -PathType Container }).Count
    csharpFiles=@($contents | Where-Object { $_.Extension -eq '.cs' }).Count
    totalBytes=($files | Measure-Object -Property Length -Sum).Sum
    categories=@($rows | Group-Object category | ForEach-Object { [ordered]@{ category=$_.Name; count=$_.Count } })
    referenceLimit='静态 GUID 与词法类型引用，仅覆盖 Assets；不是 Roslyn 语义分析或 Unity 实际依赖图。动态引用另见报告。'
    files=@($rows | Sort-Object path)
}
$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'inventory.json') -Encoding utf8
$rows | Where-Object { $_.category -eq '可以删除' } | ForEach-Object {
    if ($_.guidReferences.Count -ne 0 -or $_.lexicalSourceReferences.Count -ne 0) { throw "Cleanup candidate has static dependents: $($_.path)" }
}
foreach ($category in $summary.categories) { Write-Output ('{0}: {1}' -f $category.category,$category.count) }
Write-Output "ASSETS_AUDIT_INVENTORY_PASSED: $($contents.Count) content files classified once; $($files.Count) files including meta."
