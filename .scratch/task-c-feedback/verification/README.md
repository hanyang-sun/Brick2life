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
