# 战斗中实时编辑积木

日期：2026-10-30。战斗中允许改图；默认场景进一步采用系统固定目标与堵路保留旧路线。适用 Unity 6000.3.24f1、10×10 单层积木与编辑器双窗口。完整新规则见 [系统目标玩法](generated-defense-round-guide.md)。

## 操作

1. 进入 Play 后自动生成一个核心和 1–4 个出生点。在原始窗口摆放墙与塔，点“开始”；有自动视觉输入时等待 B 的新 RGB 稳定确认后开战。
2. 战斗进行中可以继续添加、删除和拖动积木；暂停、等待下一波时同样可编辑。必须点击原始窗口取得焦点，投影展示窗口继续只读。
3. 释放拖动、布局稳定后，B 通过 RGB 独立预测并确认，再同步地图、投影与敌人路径。默认每 0.15 秒取样，保持连续三帧一致规则。未确认时沿用上一份确认地图，操作不会通过真值直接进入战斗。
4. 墙改变通行路径；塔在确认后加入、移动或退出攻击。墙与塔暂不设数量配额；系统核心和入口不可拖动、删除或被积木占用。
5. 道路堵死或敌人无法从当前位置重规划时，面板提示沿用旧路线，继续出怪、移动和伤害；新塔/旧塔的变化仍生效。旧路线可能穿过新障碍。移除阻碍并确认后恢复新路线。结构错误（例如识别结果占据系统保留格）仍会等待修正。
6. “暂停”独立于路线警告：手动暂停后即使修复布局也保持暂停。“重试”保留本局目标和玩家布局；“生成新关卡（清空积木）”才重新生成目标。

## 保留与更新

动态更新保留同一个 BattleSimulation、敌人 ID/位置/血量、累计行进距离、波次、已出怪/击杀/漏怪计数、核心剩余生命、出怪计时、已存在塔的攻击冷却与用户暂停状态。敌人从当前位置接续，不送回入口，也不瞬移到新路线起点。

入口到核心的路线和每个存活敌人的剩余路径全部验证成功后，原子替换整组路线。路径失败时只保留最近有效路线与每个敌人的原余程，新确认的墙塔地图仍采用，不挂起战斗；显示也使用旧路线，不把部分成功路径误配到其他入口。修复后重新验证。新放置或移入的塔等待一个攻击间隔后开火；已有塔不会因无关的改图刷新冷却。

正常路线展示反映新地图；回退时展示带警告的旧路线。角色图案与射程、瞄准箭头随确认结果更新。F8 独立控制识别采集是否保留投影。

## 调用链

```text
原始窗口编辑积木
  → TopCamera RGB → FixedGridRecognizer → 连续三帧确认
  → BoardSyncSession → BoardUpdate → SimulationLevelAdapter
  → 新的已确认 LevelState
  → SimulationBattleController.SyncConfirmedLayout
  → BattleLevel.Build + BattleSimulation.TryUpdateLevel
  → 入口路径 + 每敌人当前位置的余程 + 塔与动态投影
```

控制器读取已确认 LevelState 和独立系统目标配置，不读取原始积木占用、颜色、ID 或评分结果来更新战斗。RGB 真值比较仍仅用于评价。

## 确认后的即时投影刷新

2026-10-30：`SimulationLevelAdapter.FeedbackChanged` 在采用 B 的新确认结果后，直接通知战斗控制器更新地图和投影显示，投影窗口同时请求重绘。射程圈、瞄准标记与路线几何在新地图发布时同步刷新；动画仍由 Update 推进，避免窗口先显示新积木、战斗标记还留在上一版的位置。

胜利/失败后继续编辑时，显示地图也随确认更新，保留已结束战斗的结果、核心血量与统计。“重试”用于重置战斗，不再承担投影刷新的作用。显示路线与历史战斗路线分开：进行中的回退仍用本局有效旧路线，结束后显示最新布局可用路线。

投影窗口底部提供“确认 v… · 投影 v… · 画面 v…”：分别对应 B 确认版本、源投影计划版本与窗口布局版本，稳定后应一致。仍须等待 B 的连续帧确认；单帧矩阵变化本身不表示确认完成。保留投影取样可能因动态干扰无法稳定确认，F8 不会被本次修复自动改变。

本机先验证了干净取样的战斗中自动更新；随后复现胜利后确认版本更新、射程与瞄准位置冻结的问题，并在事件直达联调时捕获新版本与旧几何相差一帧的问题。用户描述的“持续战斗中一直冻结直到重试”在当前最新 A 的默认场景中尚未原样复现，因此不能把结束状态缺陷当作该现象已证实的唯一原因。

新增 `LiveProjectionRefreshChecks` 使用真实自动 RGB，不调用 AnalyzeOnce 或 Restart：108 项通过，覆盖战斗中增墙/增塔/删塔/删墙、暂停时移塔、波次间隔增塔，以及胜利后移塔。6 次新确认在回调内立即检查完整地图同步，并核对实际窗口积木位置、源投影和射程/瞄准 Renderer 位置。原纯 C# 1076 项、关卡场景 167 项、双窗口 179 项、控制器 131 项、编辑交互 71 项也通过。

```powershell
pwsh -File Tests/RunSimulationDualWindowChecks.ps1 -CheckClass LiveProjectionRefreshChecks
```

## 验证

- 纯 C#：共 1076 项通过，含原实时改图 188 项、新系统目标 215 项、旧路回退 73 项。旧手动核心流程保持可选兼容，缺核心和堵路等待只在旧流程测试中验证。
- Unity 路线/控制器：131 项通过，确认运行中、暂停和波次间隔都使用新确认布局，仍禁止本局入口变更。
- Unity Play 编辑交互：71 项通过，实际模型写入、工具/调色按钮和清空操作可用，展示焦点仍禁止用户编辑。
- Unity 原流程真实场景：179 项通过，显式关闭系统目标后验证旧确认、核心与暂停行为；新默认玩法另有 167 项，通过实际 RGB 验证目标保护、旧路继续、多入口、塔更新和重试恢复。
- 原流程样例自动采样的新确认开战耗时约 0.320 秒，仅为本机小布局样例，不作为设备或复杂场景的时延保证。预览操作前后 RGB 差异 0 像素；无投影取样与关闭全部投影的对照差异 0 像素。

```powershell
dotnet run --project Tests/Brick2Life.Checks.csproj --no-restore
pwsh -File Tests/RunBattleControllerChecks.ps1
pwsh -File Tests/RunBattleControllerChecks.ps1 -CheckClass BattleLiveEditingChecks
pwsh -File Tests/RunSimulationDualWindowChecks.ps1
pwsh -File Tests/RunSimulationDualWindowChecks.ps1 -CheckClass GeneratedDefenseRoundChecks
```

本机结果：`.scratch/battle-controller/run-20261029-142440-676/verification-BattleControllerChecks.json`、同目录 `verification-BattleLiveEditingChecks.json`；真实场景结果和截图在 `.scratch/dual-window/run-20261029-172644-289/`，新增 `live-edited-combined.png`。

本轮保持原有四方向 A* 和固定格位识别；尚不包含真实积木抓取、遮挡处理、多层、群体避让或投影干扰准确率的成组实验。机制检索见 [研究记录](research/live-battle-editing-github.md)。
