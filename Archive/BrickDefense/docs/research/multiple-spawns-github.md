# 多出生点与相机控件精简：GitHub 检索

日期：2026-10-11。范围：玩家点选多个空导航格、各自寻路、按总数量轮流出怪；删除相机按钮，保留现有手势。验收包括零出生点、不可达/占用格、不同路线、跨波分配、战斗锁定与重试，以及宽屏/竖屏实际操作。

关键词：`github Unity tower defense multiple spawn points WaveSpawner spawners license`、`github Unity-Technologies Onslaught WaveManager spawners license`。

## 实际阅读与比较

| 候选 | 实际阅读 | 许可证与维护 | 匹配与取舍 |
| --- | --- | --- | --- |
| [Unity Onslaught](https://github.com/Unity-Technologies/Onslaught) | README.md、LICENSE.md、Assets/Scripts/Managers/WaveManager.cs、Assets/Scripts/SpawnInCube.cs；树版本 `46df271368d79a64e064713efb2cbabbe97697f4` | Unity Companion License；仓库最后推送 2019-08-23，未归档 | 波次管理器收集多个生成器并统一管理存活敌人。每个点独立增加数量，使用随机立方区域与 NavMesh；与本项目总数和格点要求不同。仅参考职责分离，保持现有生成逻辑。README 包括 Android XR，但这些历史 XR 依赖不适合整套引入 Unity 6 离线桌游。 |
| [Unity Boss Room](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop) | main 的 README.md、LICENSE.md、Assets/Scripts/Gameplay/GameplayObjects/ServerWaveSpawner.cs，2026-10-11 阅读；重点为 SpawnWave 与 SpawnPrefab | Unity Companion License；README 当前为 Unity 6000.0.52f1 / NGO 2.4.3 | 实现已出怪累计数量对出生点数量取模的轮流分配，跨波保留计数。采用该调度机制作为思路参考。完整组件需要网络、服务、NetworkObject 与协程，本项目使用纯 C# 固定时钟，不引入上述依赖。机制本身适合离线移动端，实际 Android 性能需真机验收。 |

源码入口：[Onslaught 波次管理](https://github.com/Unity-Technologies/Onslaught/blob/46df271368d79a64e064713efb2cbabbe97697f4/Assets/Scripts/Managers/WaveManager.cs)、[Boss Room 生成器](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop/blob/main/Assets/Scripts/Gameplay/GameplayObjects/ServerWaveSpawner.cs)。另检索 CelerisLab/WaveSpawnerPro；公开树仅有 README，不足以作为源码采用依据。

## 编码前整合方案

1. 两个 Unity 样例仅作机制参考，不复制/改编其源码，不引入包或资源。现有 Brackeys 的有限改编继续遵守已有第三方声明。
2. Contracts 增加只读 SpawnRoute 集合；保留单出生点接口用于已有扫描与算法检查。Domain 复用当前四方向 A*，分别选择最近可达核心；聚合结果仅在至少一个点且所有点有效时可开局。
3. Application 点选同一格切换选中状态；无效新点显示原因并保留原选择。战斗冻结所有路线；每只敌人持有所属路线，累计敌人编号轮流选择入口，跨波继续轮转，重试从首入口重新开始。每波数量为所有入口合计，核心仍共血。
4. Presentation 显示所有路线的去重预览与带编号出生标记；删除相机卡片和对应输入拦截区域，复用现有鼠标/触摸手势。
5. 在纯 C# 检查中验证分配、独立路线、非法输入、快照与锁定；在隔离 Unity 项目验证实际鼠标点击、触摸、宽/窄布局和战斗显示。
