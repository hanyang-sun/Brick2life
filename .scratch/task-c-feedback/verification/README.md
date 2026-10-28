# 任务 C 验证记录

日期：2026-10-25（Asia/Shanghai）。工程：`E:\Brick2Life`（main `691b380` + 本次改动）。

## 已执行

### 1. 纯 C# 断言

命令：

```powershell
cd E:\Brick2Life
E:\_tools\dotnet\dotnet.exe run --project Tests/Brick2Life.Checks.csproj
```

结果（全部通过）：

```text
SIMULATION_LAYOUT_CHECKS_PASSED: 325 assertions; ...
BOARD_CONTRACT_CHECKS_PASSED: 24 assertions.
BOARD_SYNC_CHECKS_PASSED: 20 assertions.
DATASET_PLAN_CHECKS_PASSED (21 assertions)
LEVEL_SEMANTICS_CHECKS_PASSED: 22 assertions.
PROJECTION_PLAN_CHECKS_PASSED: 25 assertions.
LEVEL_FEEDBACK_CHECKS_PASSED: 26 assertions.
BRICK2LIFE_CHECKS_PASSED
```

合计 463 项（原 390 + 任务 C 的 73 项）。环境：.NET SDK 10.0.200（本机临时安装于 `E:\_tools\dotnet`，未改系统 PATH）。

覆盖：颜色→角色映射、单/多核心与未映射颜色的判定、`RoleAt` 越界、规则集非法输入、格点数量与表面选择、角色高亮只覆盖占用格、变更提示的 Added/Removed/Recolored、上限截断、输出确定性、Baseline/Changed/Unchanged/Rejected/Stale 的版本与刷新语义、跨棋盘定义拒绝、探测身份不进入关卡。

### 2. 编译基线（Unity 2022.3 程序集）

本机只有 Unity 2022.3.62f3c1，工程要求 6000.3.24f1，无法用目标版本编译。用 2022.3 的模块程序集、`netstandard 2.1` 引用与内置 uGUI 源码（编译出的 `UnityEngine.UI.dll`）逐程序集编译：

```text
== UnityEngine.UI OK ==
== Core OK ==
== Application OK ==
== Simulation OK ==
== Feedback OK ==
== Editor OK ==
UNITY_BASELINE_COMPILE_OK
```

说明：这只证明语法、命名空间与所用 API 在 2022.3 的公开表面内可编译，不替代 Unity 6.3 的实际编译与运行；uGUI 版本差异（2022 的 1.x 源码 vs 工程的 2.0.0）也可能带来未覆盖的差异。

### 3. 静态核对

- `ProjectSettings/TagManager.asset`：layer 30 = `Brick2LifeGame`，layer 31 = `Brick2LifeProjection`，其余层未改动。
- `Assets/Scenes/Brick2LifeSimulation.unity`：`TopCamera` 的 `m_Bits` 由 `4294967295` 改为 `3221225471`（= 全层去掉 bit 30）；`WorkspaceCamera` 保持 `4294967263`（= 全层去掉 bit 5/UI），因此包含 30/31。
- 新增 12 个 `.meta`（Feedback 目录、5 个脚本、1 个 asmdef、Editor 安装脚本），GUID 均为随机生成，无重复。

## 4. Unity 2022.3 副本上的运行时验证（2026-10-25 21:50 前后）

目标编辑器不可得，因此把工程复制到 `E:\Brick2Life-2022`（不动仓库），仅做版本适配后在本机 2022.3.62f3c1 上真实运行：

- `ProjectSettings/ProjectVersion.txt` → `2022.3.62f3c1`；
- `Packages/manifest.json`：`com.unity.ugui` 2.0.0 → 1.0.0，删除 2022.3 不存在的 `com.unity.modules.accessibility`、`com.unity.modules.adaptiveperformance`、`com.unity.modules.vectorgraphics`；删除 `packages-lock.json` 让其重新解析。

随后用编辑器自动化脚本执行：打开 `Brick2LifeSimulation` 场景 → 调用安装菜单 `SimulationFeedbackSetup.Install()` → 进入 Play（关闭域/场景重载）→ 提交两次人工目标样例 → 采集指标与顶置相机图像。实测输出：

```text
任务 C 反馈已安装：SimulationFeedback（关卡视图、投影、适配器、人工目标演示）；顶置相机排除 Brick2LifeGame、包含 Brick2LifeProjection。
PROBE_SAMPLES_SUBMITTED sampleIndex=2
PROBE_PLAY_OK view=3 marks=134 alignErr=0 topSeesGame=False topSeesProjection=True wsSeesGame=True
  renderersGame=3 renderersProjection=134 cameraDiffProjection=25136 cameraDiffGameObjects=0
  outcome=Changed level=核心 1 · 城墙 0 · 防御塔 2 · 格点 121 · 高亮 3 · 变更 10
```

| 指标 | 实测 | 结论 |
| --- | --- | --- |
| `view` | 3 | 人工目标（核心 1 + 塔 2）在关卡视图生成 3 个游戏对象 |
| `marks` | 134 | 投影面片 = 格点 121 + 角色高亮 3 + 变更提示 10 |
| `alignErr` | 0 | 134 个面片中心与格位中心最大偏差为 0（构造对齐成立） |
| `topSeesGame` | False | 顶置相机掩码排除 `Brick2LifeGame`(30) |
| `topSeesProjection` | True | 顶置相机包含 `Brick2LifeProjection`(31) |
| `wsSeesGame` | True | 操作相机能看到游戏对象 |
| `cameraDiffProjection` | 25136 | 投影开/关时顶置 512×512 图像有 25136 像素不同 → 投影确实进入相机可见表面 |
| `cameraDiffGameObjects` | 0 | 隐藏数字游戏对象后顶置图像零像素变化 → 虚拟建筑完全不会被拍进 RGB |
| `outcome` / `level` | Changed / 核心1、塔2、变更10 | 第二次人工目标触发 Changed 与 10 个变更格 |

这次真实运行暴露并修复了两个只有运行时才会出现的缺陷（已提交）：

1. `SimulationFeedbackSetup` 在编辑模式要求 `SimulationBoardSession.IsInitialized`，而该会话只在 `Awake`（Play）初始化，导致安装菜单必然抛异常；改为通过 `SerializedObject` 读取序列化的 `palette` 生成颜色规则。
2. `ProjectionSimulator` 把 `new MaterialPropertyBlock()` 放在字段初始化器里，Unity 报 `CreateImpl is not allowed to be called from a MonoBehaviour constructor` 并打断投影刷新；改为在 `Awake` 创建，并在 `Place` 兜底。

需要强调：这是 **2022.3.62f3c1 上的运行结果**，证明逻辑、接线、分层与投影可见性成立；它不替代 `6000.3.24f1` 的目标版本验收。

## 5. 目标版本 6000.3.24f1 上的验收（2026-10-27）

编辑器由团队成员通过云主机分享的 `Unity-6000.3.24f1-Windows-x64.zip` 提供（4,876,407,494 字节，SHA-256 `0AAC9B50…A003AD`，与本机校验一致），解压到 `D:\Unity\Editors\6000.3.24f1`（49,639 个文件、7.77 GiB），`Unity.exe` 自报版本 `6000.3.24f1_4e7b9b5b6244`。

工程与编辑器版本、内置包完全对齐：`com.unity.ugui 2.0.0`、`com.unity.multiplayer.center 1.0.1` 都是 6.3 自带的内置包，因此首次导入不需要联网。

1. **目标版本编译**：`Unity.exe -batchmode -quit -projectPath E:\Brick2Life`，175.8 s，exit 0；`Library/ScriptAssemblies` 生成 `Brick2Life.Core/Application/Simulation/Feedback/Editor` 五个程序集，日志无 `error CS`。
2. **任务 C 运行时验收**（副本 `E:\Brick2Life-63test`：安装菜单 + Play + 两次人工目标 + 顶置相机对照）：

```text
PROBE_PLAY_OK view=3 marks=134 alignErr=0 topSeesGame=False topSeesProjection=True wsSeesGame=True
  renderersGame=3 renderersProjection=134 cameraDiffProjection=25136 cameraDiffGameObjects=0
  outcome=Changed level=核心 1 · 城墙 0 · 防御塔 2 · 格点 121 · 高亮 3 · 变更 10
```

与 2022.3 副本上的结果逐项一致：对齐误差 0、投影进入相机（25136 像素差异）、游戏对象对顶置图像零影响（0 像素差异）、分层掩码符合预期。

3. **Windows x64 构建**：`-executeMethod Brick2Life.Editor.Brick2LifeBuild.Windows -quit` → `Build Finished, Result: Success.`、`BRICK2LIFE_BUILD_PASSED: E:\Brick2Life\Builds\Brick2Life\Brick2Life.exe`。

至此 `docs/task-c-feedback-guide.md` 第 6 节的四项验收在目标版本上全部完成。

## 6. 接入任务 B 后的联调（2026-10-28）

分支 `feat/task-c-integration`：合并 `feat/task-b-recognition`（`3279dbb`）与任务 C，冲突仅 5 个文件（Editor asmdef、README、architecture、Tests/Program、csproj），按“双方都保留”解决；合并后纯 C# 断言 492 项通过（原 463 + Vision 29）。

改造点：

- `SimulationVisionMonitor` 暴露 `Session`、`UpdateApplied`、`SessionRebuilt`、`UiReady` 与 `FeedbackText`，并在每次 `Confirm` 后把 `BoardUpdate` 交给订阅者；新增一行“任务 C 反馈”文本；
- `SimulationFeedbackBootstrap` 发现识别面板时**共享其 BoardSyncSession**（不再自建），订阅更新并自动**停用人工目标演示**；没有面板时退回自建会话 + 演示；
- `SimulationLevelAdapter.Bind` 允许重新绑定（识别面板重建标定/会话时）并新增 `SetStatusLabel`；
- 在 `E:\Brick2Life-63int` 副本上按同一流程实测（安装菜单 → Play → 等识别稳定 → 读指标）：

```text
任务 C 已接入任务 B：共享 BoardSyncSession（calibration=fixed-grid-v1|red=224,64,64|...），识别确认后自动更新关卡与投影；人工目标演示=已停用
PROBE_INTEGRATION2_OK visionConnected=True demoEnabled=False rawScore=[occ 100/100, exact 91/100, mismatches 9]
  mismatches=[(1,1) 真值=红 预测=蓝 | (5,1) 真值=蓝 预测=红 | (8,5) 真值=红 预测=蓝 | ...]
  truthColors=[red=8 green=1 blue=1] predictedColors=[blue=8 green=1 red=1] predictedRoles=[Tower=8 Wall=1 Core=1]
  view=10 marks=140 alignErr=0 status=[关卡：核心 1 · 城墙 1 · 防御塔 8 · 格点 121 · 高亮 10 · 变更 9]
```

结论：**B→C 链路已通**（共享会话、更新转发、Demo 自动停用、状态上屏、10 个游戏对象 + 140 个投影面片、对齐误差 0）。

联调暴露并修复了一个 **B 的识别缺陷**：首次运行占用 100/100 正确，但 9 个红色格被分类成蓝色（真值 `red=8/blue=1`，预测 `blue=8/red=1`）。现场采样排除了采样位置与像素通道问题——红色积木顶面像素为 `(140,55,55)…(159,64,64)`，色相 0.00，与红色色样 0.00 一致；根因是 `FixedGridRecognizer` 只用色相（±20° 窗口 + 8° 余量），阴影下的红会漂到蓝的窗口。修复：改用“按最亮通道归一化的 RGB 色度距离”（阈值 0.35、次优差 0.12，`DecisionFraction` 0.58），保留原有的空/未知判定。修复后单帧仍有 9 格标为未知（边缘采样被阴影干扰），但连续帧稳定后的确认布局 10 格全对。

另外把安装菜单的默认角色映射从“按调色板下标”改为按语义：蓝=核心、红=防御塔、绿/黄=城墙，并在调色板没有核心色时把第一个颜色提升为核心（保证唯一核心）。

修复后同流程实测：

```text
PROBE_INTEGRATION2_OK visionConnected=True demoEnabled=False rawScore=[occ 91/100, exact 91/100, mismatches 9]
  truthColors=[red=8 green=1 blue=1] predictedColors=[red=8 green=1 blue=1] predictedRoles=[Tower=8 Wall=1 Core=1]
  view=10 marks=131 alignErr=0 status=[关卡：核心 1 · 城墙 1 · 防御塔 8 · 格点 121 · 高亮 10 · 变更 0]
```

即：识别颜色 10/10 正确、关卡合法（唯一核心）、C 生成 10 个游戏对象与 131 个投影面片、对齐误差 0、状态栏同步显示。纯 C# 断言 492 项通过（含 `VisionChecks` 29 项）。

## 7. 投影与拍照时序（2026-10-28）

新增 `IProjectionGate` + `ProjectionCaptureGate`（Simulation 程序集），`ProjectionSimulator` 实现并注册；`SimulationCameraCapture.CaptureObservationImage()` 默认“关投影 → 追加 2 帧渲染等待稳定 → 取样 → 恢复”，`CaptureObservationImage(true)` 保留投影用于干扰对照，F8 可实时切换。

Unity 6000.3.24f1 副本实测（识别稳定、投影在场时）：

```text
PROBE_CAPTURE_GATE_OK gateRegistered=True visibleBefore=True marks=131
  withFlag=True withoutFlag=False defaultFlag=False
  diffPixels=21787 restoredAfterWith=True restoredAfterWithout=True visibleFinal=True
```

| 指标 | 实测 | 结论 |
| --- | --- | --- |
| `gateRegistered` | True | 投影组件已实现 `IProjectionGate` 并注册到静态网关 |
| `marks` | 131 | 取样时投影确实有 131 个面片在场 |
| `withFlag` / `withoutFlag` | True / False | `CaptureObservationImage(true)` 带投影取样，默认取样关闭投影，标志位如实记录 |
| `diffPixels` | 21787 | 带投影与不带投影两张 512×512 取样相差 21787 像素，证明时序真的改变了相机图像 |
| `restoredAfterWith/Without` | True / True | 每次取样后投影立即恢复，不会因为取图把投影留在关闭状态 |

识别主路径现在默认使用无投影取样（对 RQ3 的反向条件用 F8 或显式重载）。

## 仍未覆盖

手册第 6 节的四项验收（安装菜单、Play、对齐、投影对照、构建）已在 6000.3.24f1 上全部完成，见第 5 节。以下仍未被覆盖，属于范围外或后续任务：

1. 真实投影仪/相机的光学时序与设备延迟——仿真投影是面片，`projectionSettleFrames` 只是仿真层等待。
2. 多层积木、遮挡与隐藏层（第一阶段明确不做）。
3. Android 等其他平台构建——分享的编辑器只带 Windows 目标。
4. 识别单帧的边缘未知格（当前 9 格），稳定帧后的确认布局正确；属 B 的后续精度优化。
5. 战斗进新管线（原缺口第 5 项）——尚未开始。

## 本次改动文件

新增：

- `Assets/Scripts/Application/LevelSemantics.cs`
- `Assets/Scripts/Application/ProjectionPlan.cs`
- `Assets/Scripts/Application/LevelFeedbackSession.cs`
- `Assets/Scripts/Feedback/Brick2Life.Feedback.asmdef`
- `Assets/Scripts/Feedback/SimulationFeedbackBootstrap.cs`
- `Assets/Scripts/Feedback/SimulationLevelAdapter.cs`
- `Assets/Scripts/Feedback/SimulationLevelView.cs`
- `Assets/Scripts/Feedback/ProjectionSimulator.cs`
- `Assets/Scripts/Feedback/SimulationFeedbackDemo.cs`
- `Assets/Editor/SimulationFeedbackSetup.cs`
- `Tests/LevelSemanticsChecks.cs`、`Tests/ProjectionPlanChecks.cs`、`Tests/LevelFeedbackChecks.cs`
- `docs/task-c-feedback-guide.md`、`docs/research/task-c-projection-github.md`
- `.scratch/task-c-feedback/`（spec、issues、本记录）
- 对应 `.meta` 文件

修改：

- `Tests/Program.cs`、`Tests/Brick2Life.Checks.csproj`（加入三组检查）
- `Assets/Editor/Brick2Life.Editor.asmdef`（引用 `Brick2Life.Feedback`）
- `ProjectSettings/TagManager.asset`（30/31 层名）
- `Assets/Scenes/Brick2LifeSimulation.unity`（顶置相机掩码）
- `docs/architecture.md`（模块、状态归属、C 小节、验证结果）
