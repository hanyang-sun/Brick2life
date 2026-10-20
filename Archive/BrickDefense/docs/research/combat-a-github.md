# 布局挑战战斗：GitHub 比较与有限适配

日期：2026-10-11。范围：静态扫描地图、四方向 A* 路线、固定塔自动攻击、手动波次、多核心共血、胜负与重试。依据 `docs/requirements-v1.md` 4.2—4.4 和本次确认的 A 方案。

检索关键词：`site:github.com Unity tower defense MIT wave spawner enemy tower`、`Brackeys Tower-Defense-Tutorial WaveSpawner Turret`、`RyanJiff WaveManager WaveSpawner`。先通过网页检索，再读取源文件；RyanJiff 网页源码获取失败后，通过 GitHub API 和本机代理取得固定树与原文。

## 实际阅读与比较

| 候选 | 阅读范围、授权 | 匹配点 | 差异与决定 |
| --- | --- | --- | --- |
| [Brackeys/Tower-Defense-Tutorial](https://github.com/Brackeys/Tower-Defense-Tutorial) | `master`，本日读取 README、LICENSE.md、`Tower Defense Unity Project/Assets/Scripts/{WaveSpawner,EnemyMovement,Enemy,Turret}.cs`；Unlicense，已读全文。未取得固定提交号，记录为分支快照。 | C# 路点跟随、最近敌人和范围检查、攻击倒计时、死亡保护、定时出怪及存活数。 | 局部适配相关计算与流程到纯 C#，不引入整套工程。原实现自动开始波次、静态全局计数、场景 Tag 搜索和金币；本项目改为手动每波、局内状态、直接敌人列表、固定积木布局。大时间步按分段路线推进，避免原距离阈值跟随可能越过路点的问题。 |
| [RyanJiff/Unity3D-TowerDefence-Template](https://github.com/RyanJiff/Unity3D-TowerDefence-Template/tree/903ed9a4b69af732e2c7c48c07cfc9647d9fbf2f) | 固定树 `903ed9a4b69af732e2c7c48c07cfc9647d9fbf2f`；README、LICENSE、ProjectVersion.txt、`Assets/Scripts/Systems/Managers/WaveManager.cs`、`Systems/WaveSystem/Wave.cs`、`Buildings/Turrets/SimpleTurret/SimpleTurret.cs`、`Enemy/Enemy.cs`。MIT，Copyright (c) 2021 Ryan Jiffri，已读全文。 | 可外部调用 StartWave；波次进度同时检查剩余出怪和存活敌人；射击间隔与显示对象分开。 | 仅采用流程思路，不复制源码。NavMeshAgent、Resources、单一 HQ、物理触发扣血与现有 A*、多核心共血不同。Wave.cs 有 UnityEditor 引用，直接导入会增加手机构建处理。README 写 2019.4.4f1，实际固定快照 ProjectVersion 为 2021.3.16f1；不能据 README 声称已兼容 Unity 6。 |

## 具体整合

- `Domain/BattleSimulation.cs` 局部改编 Brackeys 的最近目标选择、攻击冷却、路线索引和生命终止机制，保留出处并在第三方声明附 Unlicense；不导入上游素材、Unity UI 或脚本依赖。
- 本项目衔接：导航坐标中的连续位置、固定步长、只结算一次、确定的塔/敌人次序、共享核心生命、严格终局、快照与手动每波。一次射击立即结算伤害，发出显示事件；射击线和闪光不反向决定伤害。
- RyanJiff 的剩余生成数与存活数双条件作设计参考；生成间隔中暂时没有敌人不能提前结束一波。
- `Application/BattleSession` 接收当前 LevelResult，第一波开始时复制地图与配置并锁定 LevelSession。终局或放弃后释放；重试建立新状态。Presentation 接收状态和射击事件，复用软策略 UI 与地图相机。
- 不增加运行时网络、NavMesh、第三方包或新输入系统；所选运算可 Android 离线运行，实际 APK 兼容性和帧率仍须真机验证。
- 先用明确标注的试玩默认值与保守输入上限；它们不是平衡结果或 Android 性能指标。

## 验证

纯 C#：出生与间隔、跨转弯移动、最近目标/射程、冷却、死亡与漏怪互斥、多核心共血、波次双条件、手动开始、立即失败、胜利、非法/超限输入、不同帧长、锁定/重试/放弃。

Unity：原场景开局、实际对象与血条、射击效果、参数表单、宽窄屏 UI、塔射程、相机操作不改变出生点、结果与重扫入口。Android 模块缺失时明确记录编辑器验证范围。
