# 现有里程碑：GitHub 检索、检查与整合方案

日期：2026-10-08。范围：模拟扫描 → 10×10 实体格映射 → 20×20 关卡校验与四方向 A* → Unity 三维预览。本文在本次功能代码修改前完成检索与选择记录。

## 需求与验收

依据 `docs/requirements-v1.md` 第 2、4.2、5、6 节：一块积木对应四个寻路格和一个场景对象；墙与塔阻挡移动；核心为可进入目标；选择最短可达核心，等距按实体行列排序；无核心、占用出生点和无路均拒绝。需求示例为 9 块积木、36 个占格、16 步最短路线。

## 检索范围

检索关键词：`site:github.com roy-t AStar MinHeap PriorityQueue`、`site:github.com Unity grid visualization pathfinding`、`site:github.com Brackeys Tower Defense Tutorial Node.cs LICENSE`。读取仓库页面、README 和下面列出的源文件、许可证。网页缓存未返回的固定版本源码及上游许可证，通过 GitHub API 和 raw.githubusercontent.com 进一步读取；Roy 提交通过 GitHub commits 页面和 API 交叉核实。

## 候选比较

| 项目与实际阅读的资料 | 匹配与差异 | 授权与采用决定 |
| --- | --- | --- |
| [roy-t/AStar](https://github.com/roy-t/AStar)：[README](https://github.com/roy-t/AStar/blob/master/README.md)、[MinHeap.cs 固定版本](https://github.com/roy-t/AStar/blob/3d57efaa83c8cd217e4973fd4738c57e2f694bd2/Roy-T.AStar/Collections/MinHeap.cs)、[PathFinder.cs](https://github.com/roy-t/AStar/blob/master/Roy-T.AStar/Paths/PathFinder.cs)、[LICENSE 固定版本](https://github.com/roy-t/AStar/blob/3d57efaa83c8cd217e4973fd4738c57e2f694bd2/LICENSE) | C# A* 使用独立最小堆，堆只依赖 `System` 与 `System.Collections.Generic`。完整库还含图、速度、距离与单目标/接近目标路径语义，需要适配本项目的单位格代价、多核心和严格无路拒绝。 | 已阅读全文 MIT 许可证。**直接复用原版 MinHeap.cs**；A* 的堆驱动流程作思路参考，采用范围固定到提交 `3d57efaa83c8cd217e4973fd4738c57e2f694bd2`。 |
| [pixelfac/2D-Astar-Pathfinding-in-Unity](https://github.com/pixelfac/2D-Astar-Pathfinding-in-Unity)：[README](https://github.com/pixelfac/2D-Astar-Pathfinding-in-Unity/blob/master/README.md)、[Grid2D.cs](https://github.com/pixelfac/2D-Astar-Pathfinding-in-Unity/blob/master/Grid2D.cs)、[Pathfinding2D.cs](https://github.com/pixelfac/2D-Astar-Pathfinding-in-Unity/blob/master/Pathfinding2D.cs) | 节点、网格、寻路分开组织，默认四方向；网格从 Tilemap 障碍得到，依赖 Unity 世界坐标，不处理实体积木的颜色、编号或多核心。其寻路候选仍用列表，不适合替代本次选定的堆。 | 仓库页面标记 MIT，本次未取得完整许可证文本，因此**仅参考模块划分和四邻接设计，不复制或改编源码**。 |
| [Brackeys/Tower-Defense-Tutorial](https://github.com/Brackeys/Tower-Defense-Tutorial)：[README](https://github.com/Brackeys/Tower-Defense-Tutorial/blob/master/README.md)、[Node.cs](https://github.com/Brackeys/Tower-Defense-Tutorial/blob/master/Tower%20Defense%20Unity%20Project/Assets/Scripts/Node.cs)、[LICENSE.md](https://github.com/Brackeys/Tower-Defense-Tutorial/blob/master/LICENSE.md) | Unity C# 三维塔防示例；Node 负责物体创建、显示与点击。建塔、金钱、升级和预设游戏流程与本阶段需求不同，不能直接整项目移植。 | 已读 Unlicense 文本。**仅作场景展示/交互组织的思路参考，不复制源码或资产**；演示的实体来源统一改为 `LevelResult.Entities`。 |

## 有限整合方案

1. 把 Roy 原版最小堆放在 `Assets/ThirdParty/RoyTAStar/MinHeap.cs`，保留其源注释及原版实现，许可证放在同目录。本项目只调用 `Insert`、`Extract` 和 `Count`。
2. 将 A* 放到独立的 `GridPathfinder`。用不可变的候选条目保存格编号、累计代价和估计总代价；代价改善时重新入堆，取出时跳过过期条目。避免依赖上游的任意节点 `Remove` 操作。
3. 适配本项目规则：最近核心格的 Manhattan 启发式、四方向单位代价、墙/塔不可通行、全部核心目标。当找到距离 d 的核心后，继续处理估计总代价不超过 d 的候选，以完成等距核心的固定行列选择；无路返回失败，不接受上游“最接近目标”的结果。
4. `GridMapper`、示例布局和颜色/角色映射是本项目专属的数据衔接代码。在本次检查的候选中未发现相同的实体积木 2×2 映射接口，因此按已确定需求保留并加强边界校验，不采用 Tilemap 障碍检测替代识别结果。
5. Unity 场景生成消费校验后的 `LevelResult.Entities`，消除另行读取颜色映射的行为分叉。

最小堆源文件注明其来源还包括 [robin-thomas/min-heap](https://github.com/robin-thomas/min-heap)。已阅读全文 [上游 LICENSE](https://github.com/robin-thomas/min-heap/blob/master/LICENSE)：MIT，Copyright (c) 2014 Robin Thomas；该声明随 Roy 的声明一起保留。

兼容性判断：所选最小堆为纯 C#，未见平台、网络或 Unity API 依赖，因此推断可用于本项目的离线数据层；最终以本机 .NET/Unity 编译验证。Android 真机表现尚未验证。未引用候选仓库的性能宣传作为本项目性能结果。

## 检查发现与必要适配

- 原 A* 每次线性扫描列表选择候选，通用队列机制可直接使用已查验的最小堆。
- 原 `LevelBuilder` 未核对每块积木是否恰好对应其 2×2 来源格，畸形 `GridResult` 可被接受；空颜色可能在字典查找时抛异常。补充结构一致性校验，返回稳定错误代码。
- 原 Demo 直接按 `ColorRoleMap` 生成物体，绕过 `LevelResult.Entities`；改为消费关卡校验结果。
- 当前扫描模型是模拟数据子集，未实现格纸定位、置信度和照片证据。本次不据此声称真实视觉接口或手机端完整流程已经实现。

## 验证计划与实际采用

保持原需求示例及拒绝示例检查；用独立 BFS 对照随机布局的最短距离、目标排序和路线通行性；增加畸形来源格、空颜色和越界积木的拒绝检查；最后运行 Unity 批处理编译。

实际采用的代码、版本及授权见根目录 `THIRD_PARTY_NOTICES.md`。

## 完成的修改与验证结果

- 原版 `MinHeap.cs` 与 Roy、Robin 两份完整 MIT 声明已引入。`GridPathfinder` 使用该堆，保留本项目的多核心、固定等距选择和严格无路拒绝语义；未引入整个库。
- `LevelBuilder` 校验积木编号、颜色、边界、实体格重复占用和全部 2×2 来源格的一致性；畸形输入返回 `INVALID_GRID`。`GridMapper` 校验扫描编号和空白标识。
- Unity 演示改为根据 `LevelResult.Entities` 生成积木，点选出生点前检查初始化状态。
- `dotnet run --project Tests/BrickDefense.Checks.csproj` 通过。需求示例仍为 9 块积木、36 个占格、16 步；原有拒绝示例及新增缺失来源格、空颜色、越界积木检查通过。
- 固定种子 `20261008` 的 200 个布局与独立 FIFO BFS 在最短距离、目标核心和路线通行性上全部一致。
- Unity 6000.3.24f1：项目已在现有编辑器中打开，单独批处理进程未能启动。改为核对现有编辑器日志，11:39 的脚本编译显示 `Tundra build success`，没有 C# 编译错误；`Assembly-CSharp.dll` 更新时间晚于所有此次修改脚本，新增脚本及第三方文件的 `.meta` 已自动生成。

验证范围为纯逻辑检查和 Unity 编辑器编译；未进行人工场景交互验收或 Android 真机验证。
