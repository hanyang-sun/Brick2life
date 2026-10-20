# 项目已实现功能汇总

核对日期：2026-10-18（Asia/Shanghai）。依据当前磁盘代码、场景配置、采集文件和已有验证记录整理。

## 1. 当前完成到哪一步

**Brick2Life 已完成单层虚拟积木的交互搭建、固定相机取景，以及 RGB 图像与真实布局的配对导出。** 目前可以制作带格位、颜色和棋盘四角坐标的仿真样本，供后续识别算法开发和评价使用。

工程同时保留旧版“单张照片识别 → 人工校对 → 关卡生成 → 塔防战斗”原型。新仿真平台与旧识别、塔防模块尚未组成联动流程；完整投影闭环仍待开发。

| 范围 | 当前入口与状态 |
| --- | --- |
| 主 Unity 工程 | `BrickDefenseMVP/`，Unity 6000.3.24f1，Built-in 渲染管线 |
| 本次核对分支 | `feat/task-a-unity-bricks-camera`，HEAD：`597c47b` |
| 新仿真场景 | [Brick2LifeSimulation.unity](../Assets/Scenes/Brick2LifeSimulation.unity)，在 Unity 中打开后运行 |
| 旧塔防场景 | [BrickDefenseDemo.unity](../Assets/Scenes/BrickDefenseDemo.unity)，仍是 Build Settings 中启用的构建场景 |
| 历史 Windows 包 | `Builds/BrickDefense-v1.3.1-Windows-x64.zip`，对应旧塔防原型，未包含新仿真工作台 |

## 2. Brick2Life 仿真平台已实现功能

| 模块 | 已实现内容 | 当前边界 |
| --- | --- | --- |
| 固定棋盘与网格 | 10×10 棋盘，单位格边长 1；运行时绘制 22 条网格线，外边框加粗 | 当前每格最多一块单位立方体，单层 `layer=0` |
| 三维操作主视图 | 正交斜俯视显示积木顶面和侧面；根据窗口比例自动调整取景 | 尚未加入该仿真主视图的鼠标旋转、缩放控制 |
| 独立实验相机 | 固定 TopCamera 输出 RenderTexture；右侧实时显示顶置预览，默认 512×512 | 操作主视图与实验相机相互独立；界面不进入实验图像 |
| 鼠标格位定位 | 射线拾取棋盘、积木顶面和侧面；显示当前格号与选中轮廓 | 鼠标位于侧栏 UI 或棋盘外时不操作棋盘 |
| 单层积木编辑 | 选择颜色后点击空格添加；点击积木删除；拖拽并松开移动，自动对齐单位格 | 目标被占用、越界或落在 UI 上时取消移动；Esc、右键、切换工具和失焦也会取消 |
| 拖拽反馈 | 有效目标显示绿色轮廓，无效目标显示红色轮廓 | 拖动期间仅预览目标，松开前不改变真实布局和实验 RGB |
| 可配置颜色 | 场景当前配置红、绿、蓝、黄四种颜色；依据颜色 ID、中文名称和材质自动生成按钮，支持滚动浏览 | 当前颜色不绑定核心、城墙、防御塔等游戏角色 |
| 清空棋盘 | 一键删除全部积木并取消拖拽，保留棋盘、网格、相机和所选颜色 | 清空后可立即继续添加，也可导出空棋盘样本 |
| 独立真实状态 | 用 `SimulationLayout` 管理占用关系、积木 ID、颜色、格位和布局版本；移动保留 ID | 属于仿真真值，尚无从导出 JSON 恢复编辑场景的加载入口 |
| RGB 与真值导出 | 点击“保存 RGB + 标注”，生成同名 PNG 与 JSON；保存前主动渲染相机，关联帧 ID、UTC 时间、Unity 帧号和布局版本 | 当前为手动单次采集，未实现定时、批量或连续采集调度 |
| 棋盘角点数据 | 导出四个棋盘边界角点的格网坐标、世界坐标与图像像素坐标，注明顺序和像素中心约定 | 数据来自 Unity 已知几何与相机投影；真实硬件标定、图像自动角点检测尚未实现 |
| 导出一致性与失败处理 | 检查渲染期间布局版本和相机矩阵是否改变；先写临时文件，最后发布 JSON；失败清理本次创建的文件 | 提供单次配对导出的保护，尚非完整实验数据管理系统 |

固定坐标约定：逻辑 `gridX/gridY` 对应世界 `X/Z`，世界 `Y` 表示高度；棋盘原点为 `(0,0,0)`。单位积木中心为 `(gridX+0.5, layer+0.5, gridY+0.5)`。

真值 JSON 包含全部 100 格，包括空格；占用格保存 `brickId`、`colorId` 和层号。另有图像尺寸、棋盘尺寸、格边长、坐标约定和四角对应关系。它是评价算法的参考答案，当前尚无新识别模块输出的预测布局。

编辑器采集文件保存在工程的 [SimulationCaptures](../SimulationCaptures/)；Player 代码使用 `Application.persistentDataPath/SimulationCaptures/`。本次核对已有 13 对 PNG/JSON，其中 12 份较新 JSON 含四角数据，另 1 份为增加角点字段前的历史样本；全部包含 100 格且能找到对应 PNG。目录中也保留了 640×384 的旋转相机验证样本。

主要代码：

- [SimulationLayout.cs](../Assets/Scripts/Simulation/SimulationLayout.cs)：真实布局、合法格位、占用、稳定 ID 与版本。
- [SimulationBrickEditor.cs](../Assets/Scripts/Simulation/SimulationBrickEditor.cs)：增删、拖拽、颜色按钮与清空。
- [SimulationGridPointer.cs](../Assets/Scripts/Simulation/SimulationGridPointer.cs)：拾取、格号和轮廓提示。
- [SimulationWorkspaceCamera.cs](../Assets/Scripts/Simulation/SimulationWorkspaceCamera.cs)：三维主视图自动取景。
- [SimulationGridView.cs](../Assets/Scripts/Simulation/SimulationGridView.cs)：网格线与外边框。
- [SimulationCameraCapture.cs](../Assets/Scripts/Simulation/SimulationCameraCapture.cs)：图像、真值、角点配对导出与失败处理。

## 3. 旧 BrickDefenseMVP 已实现功能

以下功能保留在旧场景中，可作为后续适配基础。

| 模块 | 已实现内容 |
| --- | --- |
| 照片输入与采集 | 导入 PNG/JPG/JPEG；Unity 编辑器和 Windows Player 有系统选图窗口；已有相机启动、取景和拍照代码 |
| 无格纸照片识别 | 用户采样背景和核心、墙、塔颜色；保存/恢复标定；背景分割、颜色分类、相邻同色组件拆分与相对格位估计；保存检测框和拒绝原因 |
| 人工校对与布局设计 | 在 10×10 草稿中添加、删除、移动、改色和整体平移；撤销、恢复识别结果；确认实物真值后进入独立游戏设计阶段；可浏览三维草稿 |
| 正确率与扫描记录 | 分别计算识别正确率、格位恢复正确率；保存原图、归一化图、标定、识别证据和独立校对 JSON，保留原始识别结果 |
| 关卡生成与寻路 | 将 10×10 积木格映射为 20×20 游戏格，每块占 2×2 格；转换核心、墙、塔角色；四方向 A* 路线预览，检查缺核心、占用和无路等问题 |
| 多出生点 | 添加/取消多个入口，为各入口计算到最近可达核心的路线；敌人在入口间轮转生成，跨波继续轮转 |
| 完整塔防试玩 | 战前设置波数、数量、生命、速度、伤害和间隔；敌人沿路线移动，塔自动攻击；显示血条、射击和射程；支持共享核心生命、手动下一波、胜负、暂停、重试与返回布局 |
| 中文界面与视角输入 | 中文照片、校对、战斗和结算界面；旧场景已支持鼠标旋转/缩放及手机触摸手势，宽窄屏适配 |
| 桌面发行 | 已有 v1.3.1 Windows 试玩包及 Player 选图验证记录 |

适用范围：旧识别流程针对简单背景、尺寸一致、单层、尽量正上方完整入镜的积木照片；输出是相对布局，未恢复固定物理原点、真实毫米尺度或堆叠高度。已有实拍样本回归不能代表通用实拍准确率验收，Android 构建、触摸和相机尚需真机验收。

详细操作与模块边界见 [README](../README.md) 和 [架构说明](architecture.md)。

## 4. 独立工作区中的实验记录功能

父目录的 `work/experiment-records/` 是独立工作树，其未提交代码包含实验索引、四阶段计时、生成/出生点操作历史、实验 CSV 导出和打开记录目录等功能。它未合入本次核对的仿真分支，也不在历史 v1.3.1 试玩包内；本次未重新运行该工作区的检查。

## 5. 尚未实现或接通的功能

- 从新仿真 RGB 输出恢复固定棋盘格位、颜色的识别流程，以及与真值的自动评价。
- 对连续观测进行布局变化检测，包括增删、移动、改色和歧义处理。
- 根据视觉预测状态更新游戏内容，并建立识别、游戏与投影的联动。
- 虚拟投影作用于桌面和积木表面、投影坐标对齐及遮挡验证。
- 真实相机/投影仪标定、采集与投影开关时序、物理对齐误差及响应延迟实测。
- 多层堆叠编辑、层高估计、遮挡处理与历史状态维护。
- 仿真布局存档恢复、连续采集调度和批量实验管理。

## 6. 本次核对与验证依据

2026-10-18 重新运行现有 .NET 检查：

```powershell
dotnet run --project Tests/BrickDefense.Checks.csproj
dotnet run --no-build --project Tests/BrickDefense.Checks.csproj -- --simulation-layout-checks
```

两项均通过。旧流程检查覆盖识别、校对、关卡转换、200 个布局的独立 BFS 对照、战斗、多入口、状态回滚和拒绝处理；仿真布局专项通过 325 项断言，覆盖边界、占用、稳定 ID、不可变记录、满空棋盘与版本。

本次读取了已有 Unity 实际运行验证记录，未重新进入 Unity Play，也未操作用户当前编辑器状态：

- [三维工作台与固定相机隔离验证](../.scratch/task-a-simulation/workspace-view/verification.json)：不同窗口尺寸、独立预览与旋转操作相机后 RGB 保持一致。
- [编辑与拖拽验证](../.scratch/task-a-simulation/brick-edit/verification.json)：1024×768、1280×720 的实际编辑检查，拖拽轮廓对齐。
- [RGB/真值配对验证](../.scratch/task-a-simulation/pointer-hover/gt-paired-gt-checks.txt)：20 项检查，覆盖编辑同步、空布局、拒绝与文件清理。
- [四角坐标验证](../.scratch/task-a-simulation/pointer-hover/corners-validation-corner-checks.txt)：512×512、旋转后的 640×384、100 格颜色采样对应及相机变化拒绝。
- [清空棋盘验证](../.scratch/task-a-simulation/pointer-hover/clear-board-clear-checks.txt)：14 项检查，含同帧空布局导出和继续编辑。
- [扩展颜色与滚动验证](../.scratch/task-a-simulation/pointer-hover/palette-many-narrow-palette-checks.txt)：20 色配置和窄屏滚轮访问。

早期交接摘要中“尚无编辑、工作台和配对导出”的描述已落后于当前代码；判断当前完成情况时优先使用本汇总及对应实现、验证证据。
