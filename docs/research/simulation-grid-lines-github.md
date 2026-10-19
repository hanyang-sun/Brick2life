# 仿真棋盘网格线：GitHub 检索与有限借鉴

日期：2026-10-15。

## 本次范围

用户选择在现有工程的 Simulation 模块中学习搭建棋盘：Plane 底板、最小 X/Z 角落为原点、10×10 格、每格 1 Unity 单位。用户已经手动完成一条 LineRenderer。下一课使用循环生成两组各 11 条边界线。积木状态、鼠标编辑、相机和 Ground Truth 不属于本次实现。

验收：运行时在 Scene 窗口能看到覆盖 X/Z 0～10 的 100 格；22 条可见线，共享已配置的深灰材质；停止运行后回到手动样例。当前场景尚无相机，因此不以 Game 窗口作为本步验收入口。

## 检索与实际阅读

关键词：`github Unity grid LineRenderer grid lines MIT`、`github Unity GridSystem LineRenderer license`。

通过网页阅读两份 README；通过 GitHub API 读取下列源码、许可证和 ProjectVersion.txt。网页部分源码请求失败后，使用只读 API 成功完成核对。

| 候选 | 阅读内容 | 许可证与兼容性 | 采用判断 |
| --- | --- | --- | --- |
| [Litt1eStar/Unity-Grid_Placement_System_3d](https://github.com/Litt1eStar/Unity-Grid_Placement_System_3d)，master | README；Assets/Scripts/Grid/GridVisualizer.cs；LICENSE.txt；ProjectSettings/ProjectVersion.txt | MIT，版权行仍含 year/fullname 占位符；Unity 2022.3.8f1。源码依赖项目 GameManager、GridSystem 和 TMPro | 参考使用两个端点定义线段、将网格显示独立组织的机制。其每格四条线会重复公共边，不整包引入，也不复制代码。 |
| [Amaranthos/UnityHexGrid](https://github.com/Amaranthos/UnityHexGrid)，master | README；HexGrid/Assets/Grid.cs、Tile.cs；LICENSE；HexGrid/ProjectSettings/ProjectVersion.txt | MIT，2015 Joshua Hodkinson；Unity 2019.2.4f1。Grid.cs 的材质回退直接访问 UnityEditor.AssetDatabase，整体迁入运行时程序集需修改 | 参考通过 Inspector 指定线条材质、集中生成可视对象的机制。其六边形坐标、网格 Mesh、寻路数据不适用于本课。 |

维护信息：仅核对当前 master 文件与项目版本，未核实最近提交时间；不把候选当前可直接运行于 Unity 6.3 或 Android 视为已验证事实。

## 本项目的适配方案

- 仅借鉴机制，教学脚本独立编写，不引入外部源码或包；当前无需新增第三方代码声明。
- 使用用户已经配置好的 LineX_01 作为场景内模板，通过 Instantiate 继承其线宽、局部坐标设置和材质；不在每条线上创建 Material。
- 单个循环从 0 到 10，每次创建一条沿 X 的线与一条沿 Z 的线，避免逐格重复绘边。
- 模板保持在原来的层级中；全部克隆完成后仅关闭模板自身的 Renderer，留下 22 条可见线。
- 教学第一版只在 Start 生成，以便观察运行前后差异。保持固定原点、1 单位格长及 Y=0.01 的显示偏移，不引入编辑器自动执行或重建功能。
- 线条显示不代表已建立积木状态数据；后续的固定网格状态需单独实现。
- 仅使用 Unity 内置 LineRenderer/Instantiate，当前方案无网络或平台插件依赖；Windows、Android 实际构建尚未验证。

## 验证记录

- 已从保存的场景确认模板端点为 (0,0.01,1) 与 (10,0.01,1)，线宽 0.02，局部坐标开启，材质引用有效，父级 Transform 均为单位变换。
- 已确认材质 _Color 的 RGB 约为 0.25098，Alpha 为 1。
- 首轮记录时循环脚本尚未创建；后续已由用户编写、挂载并运行。

### 2026-10-15：预览缺线修复

- 已接入正交相机与 512×512 RenderTexture。用户确认运行中的自动生成线条仍使用旧宽度 0.02，尽管磁盘场景的模板参数已更新为 0.04。
- 生成脚本新增明确的 lineWidth=0.04 参数，在复制模板前统一赋值；沿用已有组件和循环机制，无新增第三方代码。
- 在独立 Unity 6000.3.24f1 图形进程中，故意把模板设置为旧值 0.02，再调用实际生成代码；22 条可见线的宽度均为 0.04。
- 原始图与 384×384 缩小预览检测通过；旧 0.02 宽度的 Point 缩小对照图出现缺线。证据与限制见 `.scratch/grid-render-probe/README.md`。
- 用户当前打开的编辑器需停止并重新运行，以重新生成线条。

### 2026-10-16：缩小预览的外框修复

- 实际 512×512 图仍有底边；缩小及亚像素位移后，边框会与背景混合。真实相机和 GPU 缩放检查复现了此现象，32 项顶底边检查中有 14 项变淡。
- 延用已有 LineRenderer 实现，仅增加 borderWidth=0.08；i=0/10 的四条外框加粗，内部 18 条线保持 0.04。无新增第三方代码、包或相机配置变化。
- 加粗试验及保存脚本、场景后重新进入 Play，两次均通过 32/32。教学与验证范围见 docs/task-a-grid-border-fix-guide.md。
