# 任务 B 改动说明

更新日期：2026-10-24。本文只记录相对任务 A 完成后的初始工程所做的修改。初始工程能编辑 10×10 单层积木、用顶置相机拍摄 RGB，并导出与图像配对的 Ground Truth。识别和变化事件当时尚未接入。

归档目录 `Archive/BrickDefense` 里的旧照片识别没有改，也没有接到本任务上。那套算法按画面中最左上的积木估计原点，积木一增删，其他格子会跟着平移。

## 完成的任务

从顶置虚拟相机的 RGB 图像恢复固定 10×10 布局，并在布局稳定后输出增加、删除和移动。

- 单帧预测：每个格子是空，或是红、绿、蓝、黄中的一种。坐标使用场景里固定的棋盘原点，不从图像里重新找原点。
- 真值比对：预测完成之后，再与仿真棋盘的当前布局比较，得到准确率。真值不进入识别函数。
- 动态事件：连续 3 帧的 100 格完全一致后才记录。第一次稳定结果只建立基线。之后同色且唯一的一减一增记为移动；同色有多种配法时只记增加和删除。

界面上的格位从 1 显示到 10。程序内部和保存的标注 JSON 仍从 0 计到 9，因此不会和相机角点、真值错一位。左下角在界面上是 `(1,1)`。

## 新增文件

这些文件在初始工程中不存在。

| 路径 | 作用 |
| --- | --- |
| `Assets/Scripts/Vision/Brick2Life.Vision.asmdef` | 无 Unity 依赖的识别程序集 |
| `Assets/Scripts/Vision/RgbImage.cs` | 顶左原点的 RGB 图 |
| `Assets/Scripts/Vision/VisionCalibration.cs` | 背景与四种积木的公开颜色标定，以及顶面采样参数 |
| `Assets/Scripts/Vision/CameraProjection.cs` | 用相机矩阵把顶面采样点投到像素 |
| `Assets/Scripts/Vision/FixedGridRecognizer.cs` | 单帧 100 格预测 |
| `Assets/Scripts/Vision/LayoutChangeTracker.cs` | 连续帧确认，以及增加、删除、移动事件 |
| `Assets/Scripts/Vision/RecognitionScore.cs` | 预测与真值逐格比较 |
| `Assets/Scripts/Simulation/SimulationObservationImage.cs` | 一次拍摄的 RGB 和相机矩阵，不含真值 |
| `Assets/Scripts/Simulation/SimulationVisionMonitor.cs` | 定时拍摄、调用识别、显示面板 |
| `Tests/VisionChecks.cs` | 纯 C# 识别与事件检查 |
| `Tests/Unity/VisionRecognitionChecks.cs` | 用真实场景相机渲染的检查 |
| `Tests/RunVisionRecognitionChecks.ps1` | 在隔离 Unity 工程中运行上述检查 |
| `docs/research/fixed-grid-recognition-github.md` | 编码前的检索与取舍 |

`.scratch/task-b-recognition/` 只保存本任务的规格记录。`.scratch/vision-recognition/run-*` 是隔离检查的临时工程，不属于产品代码。

## 修改过的原有文件

只增加了识别所需的入口、显示编号和检查工程的编译范围。棋盘尺寸、积木摆放、相机机位、光照和 Ground Truth 的 JSON 结构没有改。

| 原有文件 | 改动 |
| --- | --- |
| `Assets/Scripts/Simulation/SimulationBootstrap.cs` | 启动时绑定识别面板 |
| `Assets/Scripts/Simulation/SimulationCameraCapture.cs` | 增加 `CaptureObservationImage` 和 `BuildTopSamples`。原有的「保存 RGB + 标注」仍按原格式写 PNG/JSON |
| `Assets/Scripts/Simulation/SimulationBrickEditor.cs` | 状态文字中的格位改为从 1 显示 |
| `Assets/Scripts/Simulation/SimulationGridPointer.cs` | 「当前格位」改为从 1 显示。拾取和世界坐标换算未改 |
| `Assets/Scripts/Simulation/Brick2Life.Simulation.asmdef` | 增加对 Application、Vision 的引用 |
| `Assets/Editor/Brick2Life.Editor.asmdef` | 增加对 Vision 的引用 |
| `Tests/Program.cs`、`Tests/Brick2Life.Checks.csproj` | 纯 C# 检查加入 Vision |
| `Tests/RunSimulationCameraMetadataChecks.ps1`、`Tests/RunSimulationLightingChecks.ps1`、`Tests/GenerateDataset.ps1` | 隔离工程复制脚本时带上新建的 Vision 目录，避免原有检查编译失败 |
| `README.md`、`docs/architecture.md` | 说明 Vision 已接入 |

未改的部分包括：`Assets/Scripts/Core` 的棋盘合同、`Assets/Scripts/Application/BoardSyncSession.cs` 的确认规则、场景里的相机与材质、`Archive/BrickDefense`。

## 实现方式

1. 拍摄顶置相机，得到 RGB。同时用 Unity 的 `Camera.WorldToViewportPoint` 把每一格顶面中心（世界高度为 1）投到像素上。格子在世界中的位置来自固定棋盘，不是 Unity 的识别组件，也不读取积木 Transform。
2. 每个像素按色相与红、绿、蓝、黄比较。低饱和度视为空。一格 3×3 个采样里多数一致才给出预测。看不清则为未知，不当成空格。
3. 预测得到 100 格之后，读取仿真棋盘快照作为真值，逐格比较占用和颜色。面板上的「单帧」是这一帧的预测；「确认」是连续 3 帧稳定后的结果。
4. 稳定结果相对上一份基线产生事件。界面只负责显示。识别面板不进入顶置相机，因此打开面板不会改变被识别的画面。

## 效果

2026-10-24 在 Unity 6000.3.24f1 的隔离工程中，用场景里的真实顶置相机渲染：

- 单帧准确率和确认准确率均为 100/100。
- 播放中增加绿色、删除、移动红色，再在角格增加黄色，事件格位和颜色与操作一致。
- 相机矩阵投影和 `WorldToViewportPoint` 的像素差小于 0.001。
- 变化事件超过 8 条时可以滚动查看，事件区域不挡住工作台的左键操作。
- 结果记录在 `.scratch/vision-recognition/run-20261024-111608-346/verification.json`。

同一天 `dotnet run --project Tests/Brick2Life.Checks.csproj` 通过。其中识别相关 29 项，覆盖四角、相邻同色、亮度变化、未知颜色、单帧抖动，以及增加、删除、移动和同色歧义。原有布局、合同、确认会话和数据集检查仍通过。

本次验收的是虚拟顶置相机下的单层积木。真实相机、遮挡和投影干扰不在这次结果内。

## 如何演示

1. 用 Unity 6000.3.24f1 打开本工程，场景选 `Assets/Scenes/Brick2LifeSimulation.unity`，点击 Play。
2. 等待约一秒。画面中央出现识别面板，矩阵和准确率分开显示。开局事件应为「尚无增删移动」。
3. 屏幕顶部的「关闭识别」可以收起面板，再点「识别结果」打开。面板半透明。除该按钮外，左键可以点穿面板去操作棋盘。
4. 在左侧「积木编辑」中选择颜色并添加、删除或拖动积木。矩阵马上变化。约一秒后，事件区出现对应的增加、删除或移动。
5. 事件变多时，把鼠标放在「变化事件」文字上，用滚轮或按住中键上下查看更早的记录。左键仍然操作积木。
6. 右侧「固定相机预览」是识别实际使用的图像。需要离线样本时，仍用原来的「保存 RGB + 标注」。
