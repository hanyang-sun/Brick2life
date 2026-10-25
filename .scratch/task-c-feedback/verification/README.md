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

## 未执行（缺少 Unity 6000.3.24f1）

1. 在目标编辑器中编译与进入 Play。
2. 面片与格位对齐的实测（`MeasureAlignmentError` 的运行值）。
3. 投影开/关的顶置相机图像对照，以及顶置相机是否完全不拍到游戏对象。
4. `Brick2Life/Simulation/Install feedback` 与 `Verify feedback layering` 的实际执行。
5. Windows x64 Player 构建（`Brick2Life/Build/Windows x64`）。

以上均记录在 `docs/task-c-feedback-guide.md` 第 6 节，供有 Unity 6.3 的机器按步骤核对；在完成前，任务 C 只能声明“逻辑与规则已验证、引擎接线待实际运行验收”。

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
