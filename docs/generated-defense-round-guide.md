# 系统目标与实时建造玩法

更新：2026-10-30。适用 Unity 6000.3.24f1、默认 `Brick2LifeSimulation` 场景、编辑器双窗口。

## 当前规则与操作

1. 进入 Play，系统随机生成 **一个核心、1–4 个敌人出生点**。核心位于内侧，出生点位于棋盘边缘，互不重合；数量可以是一，也可以是多个。场景的 `minimumSpawns/maximumSpawns` 可调整范围。
2. 原始布局窗口显示蓝色核心标记、金色入口圈和编号。它们是固定系统目标，不能被玩家删除、拖动，也不能在对应格放置积木。展示窗口显示同一组目标。
3. 玩家摆放绿色城墙、红色防御塔；暂不设数量配额，仍受 10×10 棋盘、一格一块和目标保留格限制。清空只删除玩家积木。
4. 点击“开始”，自动识别开启时等待新 RGB 的稳定确认后开战。战斗、暂停和波次间隔中都能增删、移动墙塔；必须点击原始窗口取得编辑焦点，展示窗口保持只读。
5. B 确认新布局后重新计算入口路线和存活敌人从当前位置出发的余程。若堵路，提示“沿用上次有效路线（可能穿过新放置的积木）”，**战斗继续**，新墙不撤销，塔的增删仍生效。这里确实允许敌人穿过后来放在旧路径上的积木，这是当前约定的回退规则。
6. 恢复通路并确认后重新规划，警告消失。若只是墙暂时压在敌人脚下、静态地图仍有通路，则按模拟时间每 0.25 秒尝试恢复；敌人离开障碍后，无需再次改动积木。手动暂停独立保留，修好路不会自动解除暂停。只有结构错误（例如识别结果占据系统保留格）进入等待修正；普通堵路不进入等待。
7. 一波结束后点击“下一波”；守住全部波次胜利，核心生命为 0 失败。“重试”保留核心、入口和玩家布局，重置战斗；“生成新关卡（清空积木）”重新生成目标并清空玩家积木，仅在未战斗且未等待开战确认时可用。

准备阶段已经堵路时，首次回退使用本局生成时的无玩家障碍参考路线；之后保留最近一次全部可行的路线组。多个入口整组更新，敌人持续按原入口索引轮转，不用候选路线的部分成功结果替换入口编号。

## 识别边界与显示

系统目标是公开关卡配置，和玩家积木预测分开：B 仍只接收 RGB 与公开标定配置，不读 JSON 中的占用、颜色或 ID。`LevelState` 在应用层合入固定系统核心，但核心所在空格的 `Occupied=false`，不会伪造蓝色积木观测。无障碍参考关卡只用于初始导航，不提交给 `BoardSyncSession` 或识别评分。

原始目标提示位于 `Brick2LifeObjectives`（29）层，无 Collider；游戏对象位于 30 层，顶置相机同时排除这两层。真正的投影位于 31 层，包含桌面的核心徽记、入口圈及墙塔图案，可以进入带投影 RGB。默认识别取样仍临时关闭全部已注册投影，F8 切换是否保留投影；展示窗口模式不改变这个实验条件。当前 B 接入后人工演示组件自动停用，F5/F6/F7 不是默认玩法操作入口。

识别默认每 0.15 秒取样，连续三帧一致后确认；同一帧重复提交不累计，标定切换重新建立确认过程。实验效果仍取决于实际 RGB 预测是否稳定，系统目标配置不会绕过玩家积木确认。

## 具体文件与调用链

| 文件 | 本轮职责 |
| --- | --- |
| `Assets/Scripts/Application/GeneratedRound.cs` | 固定目标、确定性随机生成、保留格、初始导航参考 |
| `Assets/Scripts/Application/LevelSemantics.cs`、`LevelFeedbackSession.cs` | 确认积木与系统目标合入关卡，检查保留格冲突 |
| `Assets/Scripts/Feedback/SimulationBattleController.cs` | 生成/重试、固定目标、确认后更新、最近有效导航 |
| `Assets/Scripts/Battle/BattleLevel.cs`、`BattleSimulation.cs` | 20×20 寻路，完整入口集合，路径回退与塔更新 |
| `Assets/Scripts/Simulation/SimulationBoardSession.cs`、`SimulationBrickEditor.cs` | 保护目标格，只提供墙塔，保持实时编辑 |
| `Assets/Scripts/Feedback/SimulationRoundMarkers.cs` | 原始窗口目标提示，资源复用与清理 |
| `Assets/Scripts/Application/ProjectionPlan.cs`、`Feedback/ProjectionSimulator.cs` | 桌面核心/入口投影，清除旧面片时立即停显 |
| `Assets/Editor/SimulationProjectionPreview.cs` | 独立展示目标，不重复镜像原始提示几何 |
| `Assets/Editor/SimulationFeedbackSetup.cs`、场景、`ProjectSettings/TagManager.asset` | 新默认规则与分层接线 |

```text
GeneratedRound → 固定目标 / 编辑保留格 / 原始提示
                       ↓ 已知关卡配置
TopCamera RGB → B 预测与连续帧稳定 → BoardSyncSession → BoardUpdate
  → SimulationLevelAdapter / LevelFeedbackSession → LevelState
  → BattleLevel.Build → BattleSimulation.TryUpdateLevel
       ├─ 全部路径可行：更新路线、敌人余程与塔
       └─ 路径失败：保留完整旧路线，采用新墙塔，警告并继续
  → 投影计划 / 战斗显示 / 只读展示窗口
```

复现实验可调用 `controller.GenerateNewRound(seed: 2, spawnCount: 3)`；须在允许生成新关卡的状态调用。控制台记录种子、实际核心和入口坐标，面板显示种子、核心及入口数量。应同时记录 `GeneratedRound.GeneratorVersion`（当前 `generated-round-v1`）、棋盘定义、种子和入口数量，不把不同生成版本当作相同实验。

## 已验证与验收

2026-10-30 本机结果：

- 纯 C# **1076 项**通过，含新增目标生成 215 项、堵路回退 73 项；覆盖确定性、1/2/多个入口、目标互异、初始可达、部分入口堵路、敌人脚下障碍、塔更新、暂停与恢复。
- Unity 新规则真实场景 **167 项**通过，使用实际 RGB 预测与稳定确认，覆盖单/多入口、60 块玩家积木、保留格保护、战斗堵路继续、修复重规划、无新布局版本的障碍脱离恢复、准备期堵路开战、重试路线恢复、核心受伤桌面反馈和双窗口只读。
- Unity 原流程双窗口 **179 项**、控制器 **131 项**、实时编辑 **71 项**通过。旧流程测试明确关闭 `generateObjectives`，保留手动核心与布局等待行为的兼容检查。
- 新规则样例干净 RGB 整盘精确识别率 **100%**；目标提示开/关、预览操作前/后对源 RGB 的变化均 **0 像素**。这些是当前仿真样例的回归结果，不代表带投影或真实设备的总体准确率。

```powershell
dotnet run --project Tests/Brick2Life.Checks.csproj --no-restore
pwsh -File Tests/RunSimulationDualWindowChecks.ps1 -CheckClass GeneratedDefenseRoundChecks
pwsh -File Tests/RunSimulationDualWindowChecks.ps1
pwsh -File Tests/RunBattleControllerChecks.ps1
pwsh -File Tests/RunBattleControllerChecks.ps1 -CheckClass BattleLiveEditingChecks
```

本机新规则结果：`.scratch/dual-window/run-20261029-172644-289/verification-GeneratedDefenseRoundChecks.json`；同目录 `generated-round.png`、`blocked-fallback.png` 可查看效果。

复用成熟部分：现有固定格位识别、确认会话、四方向 A*、固定步长战斗与双窗口。新增目标生成和旧路回退已通过上述仿真验证。关卡难度平衡、数量经济、真实投影干扰成组实验、多层及实体设备仍需后续验证；本轮没有重新执行 Windows Player 构建。机制比较与授权记录见 [编码前研究](research/generated-defense-round-github.md)。
