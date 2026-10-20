# BrickDefense 退役清理范围

日期：2026-10-20。状态：已完成引用核对与范围整理，尚未移动或删除源码、资源和测试。

## 原则与核对结果

主工程只维护 Brick2Life。旧系统整体归档到 Unity 导入目录之外，历史版本不再参与当前编译、构建、默认检查或功能规格。以后确有需要时，只迁入具体算法与必要依赖。

- Simulation 有 9 个运行时脚本，没有旧 BrickDefense 命名空间引用；程序集只显式引用 UnityEngine.UI。
- 从 Brick2LifeSimulation 场景递归核对本地资源 GUID，没有引用旧运行时脚本。
- 活动目录有 33 个旧运行时脚本、9 个旧编辑器脚本，另有旧第三方算法源码。
- 新 SimulationLightingSetup 位于旧编辑器程序集下，需要拆开这一编译关系。

静态核对支持整体归档建议，不代替 Unity 编译、场景运行和构建验收。

## 归档清单

| 范围 | 文件或目录 | 处理 |
| --- | --- | --- |
| 旧运行时 | Assets/Scripts 下 Contracts、Domain、Vision、Application、Infrastructure、Presentation 六个目录 | 整体归档，包含程序集与 .meta |
| 旧场景 | Assets/Scenes/BrickDefenseDemo.unity | 连同 .meta 归档，构建入口切换到新仿真场景 |
| 旧编辑器 | Assets/Editor 下除 SimulationLightingSetup 外的 9 个 .cs，以及旧编辑器程序集 | 整体归档；为新工具建立独立 Brick2Life 编辑器程序集 |
| 旧算法依赖 | Assets/ThirdParty 下 RoyTAStar、Colourful、AssemblyInfo.cs、BrickDefense.Algorithms.asmdef | 随旧代码、完整授权及 .meta 归档 |
| 旧扫描资源 | Assets/Resources/photo-scan-settings.json、BrickSurface.mat | 旧运行时移出后，复核无剩余动态加载再归档 |
| 旧测试 | 战斗、多出生点、寻路、照片识别、校对和流程检查，旧测试工程及 Tests/Fixtures | 整体归档；从混合 Program 提取新主线入口 |
| 旧文档 | 旧照片/校对/塔防规格、v1.3 发布记录及对应研究 | 放入历史资料范围，退出当前需求和架构入口 |
| 历史产物 | Builds 内旧包、根目录旧验证日志 | 放入独立历史产物目录 |

不以批量改名替代退役；旧系统改名后仍会保留原有职责和维护成本。

## 必须保留

- Assets/Scripts/Simulation 的 9 个脚本、程序集及 .meta。
- SimulationLightingSetup 及 .meta，接入独立新编辑器程序集。
- Brick2LifeSimulation 场景、Simulation 材质、TopCameraRGB 及对应 .meta。
- NotoSansSC 中文字体、随包授权及 ThirdParty/NotoSansSC 的来源记录。新场景确实引用字体，不能随旧 UI 清理。
- SimulationLayoutChecks、两个 Unity 仿真检查与对应运行脚本。
- 新主线的相机、光照、网格、工作台说明和研究；已撤回的大规模测试集研究明确保留历史状态。
- 用户现有 SimulationCaptures、实验验证证据与工作记录，不列入本轮默认清理目标。
- Unity 项目设置、官方 UI 包和仍在使用的授权。

## 配套修改与验收

1. 建立 Brick2Life.Checks.csproj 与新 Program，默认执行现有 325 项布局检查；旧测试不改名保留为默认检查。
2. Unity 检查脚本只复制新场景、新工具及真实依赖。
3. Build Settings 切换到新仿真场景，完成实际 Player 构建验证。
4. 主 README 和 architecture.md 专门描述 Brick2Life 现状、启动方式及待接通链路。
5. 第三方声明保留字体和 uGUI；旧算法授权随旧实现归档。
6. 修复文档链接；代理流程中对旧 requirements-v1 的默认指向应另行更新为当前规格。

移动前保存当前磁盘内容、未提交修改、文件清单与校验值，不能只依赖旧标签。资源与 .meta 配对；递归移动前验证源/目标绝对路径位于项目或明确的归档范围。不改变用户未保存的 Unity 状态。

清理后检查活动程序集、默认场景与默认测试入口，再运行布局检查、Unity 编译、编辑/采集检查和 Player 构建。本轮不扩展识别、多层或投影功能。
