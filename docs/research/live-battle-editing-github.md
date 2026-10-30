# 战斗中编辑积木与动态寻路：GitHub 检索

日期：2026-10-30。用户明确将旧的战斗禁改规则改为：关卡进行时仍可放置、删除和移动积木。保留双窗口只读展示与编辑焦点约束；变化经 B 的 RGB 稳定确认进入战斗，不读取原始布局真值。

## 检索与阅读

关键词：`Unity tower defense FindPaths ToggleWall GameBoard`、`Unity dynamic pathfinding grid update runtime tower defense`、`SebLague Pathfinding Unit UpdatePath`。检索后实际读取以下两个项目的 README 和实现，检查根目录许可证。

| 候选 | 读过的实现与授权 | 比较与决定 |
| --- | --- | --- |
| [Oppressor2045/Unity-3D-Tower-Defense-Pathfinding](https://github.com/Oppressor2045/Unity-3D-Tower-Defense-Pathfinding) | 本轮树 `0f31fa9d44510be45e87e86b4c5823656f6ede95`；README、PathManager.cs（blob `337d9e44d384b0d9255c16dc069d9c9d455c3d3c`）、EnemyMover.cs（`e759ea558b49194ffca0e35b2cbfa660c637e1b7`）。根树未提供 LICENSE，不复制代码。 | 放置后通知重算的机制接近需求，但实际 EnemyMover 接收的是全局起点路径，未实现 README 所述的每敌人当前位置重算，直接使用会让敌人折返入口；依赖 Unity Physics 的占用扫描也不符合 RGB 独立预测合同。只参考“确认变化触发重算”的机制。 |
| [KingSlme/unity-a-star-pathfinding](https://github.com/KingSlme/unity-a-star-pathfinding) | 本轮树 `3f1f9a18ae02b3e0c1a0f0a3dff984f67c72c455`；README、A Star 3D/Grid3D.cs（`f9061fac691e6a4f0f02bc95840ece381eb12ce3`）、Examples/FollowPath3D.cs（`1711a5655688f94ff1841d517d50ddf3244a3d5f`）、LICENSE（MIT，2023 KingSlme）。 | 动态刷新和从 Transform 当前位置找路可参考，但依赖物理扫描、三维邻居与每帧重算；没有确认状态、战斗状态保留、堵路恢复。仅参考当前位置接续和目标变动的重算时机，不引入源码。 |

两者都是 Unity C#，不需在线服务，但没有目标 Unity 6000.3 或 Android 的本项目验证；全量接入会增加引擎耦合。本项目已有 Unity-free 四方向 A*、最多 20×20 导航格和确定性战斗，直接扩展现有实现更合适。无新增第三方依赖或源码复制。

## 实施方案

1. BattleSimulation 增加原子布局更新：先验证所有入口路径和每个存活敌人当前位置的后续路径，全部成功才替换地图和塔。敌人位置、ID、血量、波次、计时、核心生命与用户暂停状态保持。
2. 每个敌人拥有自己的剩余路径，用逐段距离推进，重新规划不重置到入口、不瞬移；当前位置落入新障碍时挂起，提示移开障碍。
3. 无核心、多核心、入口被占或堵路时，进入独立的“等待有效布局”状态，停止出怪、移动和伤害；修复并确认后恢复，不覆盖用户暂停状态。
4. Controller 只观察 C 从 BoardUpdate 生成的确认 LevelState，按新版本更新战斗与路径显示。释放旧的战斗编辑锁；出生点仍在开战前配置，原始窗口焦点继续限制用户编辑。
5. 动态投影跟随新关卡更新，无有效通路时隐藏旧路线；显示明确的等待原因。原有 F8 实验条件保持。

验证覆盖纯 C# 状态保留、绕行、移动核心、塔变化、堵路与恢复、敌人脚下障碍、暂停与波次间隔，以及真实 Unity 中编辑和 RGB 确认后的同步。此功能改变旧“战斗冻结地图”约定，相关测试与文档一并更新。

实施后：788 项纯 C#、131 项 Unity 控制器、71 项实时编辑 Play、178 项真实场景检查通过。实际阅读指出的“全局起点路径被重新发给所有敌人”的问题没有沿用，存活敌人各自从连续位置重算；失败挂起与用户暂停分别管理。当前用法和证据见 [实时交互指南](../live-battle-editing-guide.md)。
