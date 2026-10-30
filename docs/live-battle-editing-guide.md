# 战斗中实时编辑积木

日期：2026-10-30。用户最新要求替代此前“战斗期间禁止改图”的约定。适用 Unity 6000.3.24f1、10×10 单层积木与编辑器双窗口。

## 操作

1. 在原始积木布局窗口摆放积木并选择出生点，点“开始”；有自动视觉输入时等待 B 的新 RGB 稳定确认后开战。
2. 战斗进行中可以继续添加、删除和拖动积木；暂停、等待下一波时同样可编辑。必须点击原始窗口取得焦点，投影展示窗口继续只读。
3. 释放拖动、布局稳定后，B 通过 RGB 独立预测并确认，再同步地图、投影与敌人路径。默认每 0.15 秒取样，保持连续三帧一致规则。未确认时沿用上一份确认地图，操作不会通过真值直接进入战斗。
4. 墙改变通行路径；塔在确认后加入、移动或退出攻击；移动核心会改变目标位置，但不会补满核心生命。
5. 若缺少唯一核心、入口被占、道路完全堵死或积木占到敌人当前位置，战斗面板提示“等待修正布局”，停止出怪、移动和伤害。此时编辑仍可用，修复并确认后继续本局。
6. “暂停”独立于布局等待：手动暂停后即使修复布局也保持暂停；点击继续后推进。出生点在本局固定，结束或重试后再改。

## 保留与更新

动态更新保留同一个 BattleSimulation、敌人 ID/位置/血量、累计行进距离、波次、已出怪/击杀/漏怪计数、核心剩余生命、出怪计时、已存在塔的攻击冷却与用户暂停状态。敌人从当前位置接续，不送回入口，也不瞬移到新路线起点。

入口到核心的路线和每个存活敌人的剩余路径全部验证成功后，才原子替换地图与塔；失败时保留内部上一份可用地图并挂起战斗，隐藏过时的路线提示。修复后重新验证。新放置或移入的塔等待一个攻击间隔后开火；已有塔不会因无关的改图刷新冷却。

路线展示反映新地图上的入口路径，存活敌人各自计算从当前连续位置出发的剩余路径。角色图案与射程、瞄准箭头随确认结果更新。F8 仍独立控制识别采集是否保留投影。

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

控制器只读取已确认 LevelState，不读取原始积木占用、颜色、ID 或评分结果来更新战斗。RGB 真值比较仍仅用于评价。

## 验证

- 纯 C#：原有 600 项及新增实时改图 188 项，共 788 项通过。覆盖连续位置绕行、移动核心、波次和血量保留、塔增删和冷却、缺核心、堵路、入口被占、敌人脚下障碍、挂起恢复与用户暂停。
- Unity 路线/控制器：131 项通过，确认运行中、暂停和波次间隔都使用新确认布局，仍禁止本局入口变更。
- Unity Play 编辑交互：71 项通过，实际模型写入、工具/调色按钮和清空操作可用，展示焦点仍禁止用户编辑。
- Unity 真实场景：178 项通过，使用实际顶置 RGB 验证新增墙、移动塔、删除墙/核心、恢复和移动核心、替换塔；单帧尚未确认时不更新地图，确认后保留同一战斗和敌人位置。
- 本轮自动采样的新确认开战耗时约 0.317 秒，仅为本机小布局样例，不作为设备或复杂场景的时延保证。预览操作前后 RGB 差异 0 像素；无投影取样与关闭全部投影的对照差异 0 像素。

```powershell
dotnet run --project Tests/Brick2Life.Checks.csproj --no-restore
pwsh -File Tests/RunBattleControllerChecks.ps1
pwsh -File Tests/RunBattleControllerChecks.ps1 -CheckClass BattleLiveEditingChecks
pwsh -File Tests/RunSimulationDualWindowChecks.ps1
```

本机结果：`.scratch/battle-controller/run-20261029-142440-676/verification-BattleControllerChecks.json`、同目录 `verification-BattleLiveEditingChecks.json`；真实场景结果和截图在 `.scratch/dual-window/run-20261029-172644-289/`，新增 `live-edited-combined.png`。

本轮保持原有四方向 A* 和固定格位识别；尚不包含真实积木抓取、遮挡处理、多层、群体避让或投影干扰准确率的成组实验。机制检索见 [研究记录](research/live-battle-editing-github.md)。
