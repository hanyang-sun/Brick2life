# 任务 A：三维工作台与固定相机小预览

本步采用用户于 2026-10-16 确认的正交斜俯视。主视图能显示积木顶面和侧面；相机控制与积木编辑后续逐步加入。

## 场景保存在哪里

正式配置保存在 `Assets/Scenes/Brick2LifeSimulation.unity`。本步沿用原有棋盘、网格、积木、材质和灯光，在原场景内增补操作相机及三个中文说明对象。

```text
SimulationRoot
├── Board
├── GridLines
├── BricksRoot
├── TopCamera                 固定实验相机，输出 TopCameraRGB
└── WorkspaceCamera           操作相机，输出屏幕左侧主区域

SimulationCanvas             Screen Space Overlay
├── PreviewBackground         右侧背景
├── PreviewArea
│   └── CameraPreview         显示 TopCameraRGB，保持 1:1
├── WorkspaceTitle            “积木工作台”
├── PreviewTitle              “固定相机预览”
└── PreviewDescription        “用于后续图像识别”
```

WorkspaceCamera 带 Camera、AudioListener 和 SimulationWorkspaceCamera。它的 MainCamera 标签用于明确场景的主要相机；后续鼠标编辑仍应显式指定相机引用。

TopCamera 改为 Untagged，移除 AudioListener；它的位置、旋转、正交尺寸和目标纹理保持原配置。两台相机均是 SimulationRoot 的直接子对象，改变操作相机不会带动实验相机。

## 相机与界面怎样配合

| 对象/参数 | 本步配置 | 作用 |
| --- | --- | --- |
| WorkspaceCamera 投影 | Orthographic | 同尺寸积木不因远近缩放 |
| WorkspaceCamera 旋转 | X=55°、Y=-45°、Z=0° | 从斜上方观察棋盘，能看到顶面及侧面 |
| WorkspaceCamera 朝向目标 | 世界坐标 (5,0,5)，距目标 22 | 对准棋盘中心，物体位于裁剪范围内 |
| WorkspaceCamera Viewport Rect | X=0、Y=0、W=0.72、H=1 | 左侧 72% 为操作区域 |
| WorkspaceCamera Target Texture | None | 直接输出到 Game 窗口 |
| WorkspaceCamera 正交尺寸 | 编辑状态初值 8.2；运行时自动取景 | 适应当前窗口比例 |
| WorkspaceCamera Near/Far | 0.3 / 60 | 覆盖当前工作台 |
| PreviewBackground | 锚点 (0.72,0)～(1,1) | 背景限制在右侧，不覆盖工作台 |
| PreviewArea | 锚点 (0.72,0.45)～(1,1)，左右留白 18、顶部留白 112（Canvas 单位） | 限定侧边预览空间，避免不同窗口比例下图像挤到标题 |
| CameraPreview | 原纹理、FitInParent、Aspect Ratio=1 | 缩小显示实验图像，纹理本身仍是 512×512 |

主视图链路：三维物体 → WorkspaceCamera → 屏幕主区域。

实验图像链路：同一组三维物体 → TopCamera → TopCameraRGB → CameraPreview。

Canvas 使用屏幕叠加模式，中文说明与背景不进入实验纹理。三个说明使用现有 Noto Sans SC 字体；它们不接收射线，也没有新增按钮或 EventSystem。

## 新运行脚本怎样工作

文件：`Assets/Scripts/Simulation/SimulationWorkspaceCamera.cs`。它与 SimulationGridView 同属于现有 Brick2Life.Simulation 程序集；未新增程序集或包引用。SimulationGridView 负责画网格，本脚本仅负责操作相机的取景。

**变量：**

- `padding` 默认 1.1，为计算出的视野增加 10% 余量。数值越大，棋盘四周留白越多；Inspector 限制它至少为 1。
- `minimumSize` 默认 7.1，是允许的最小正交半高，防止宽窗口下画面过度放大。增大它会让棋盘看起来更小。
- `workspaceCamera` 缓存同一个 GameObject 上的 Camera，后续每帧直接使用该引用。
- `halfWidth` 与 `halfHeight` 是当前工作台在相机坐标中的最大横向、纵向距离，不是棋盘的世界坐标宽高。

**执行过程：**

1. `Awake()` 在进入 Play 时取得本对象的 Camera。
2. `LateUpdate()` 检查单层范围 X/Z=0～10、Y=0～1 的八个角点。用 `InverseTransformPoint` 把世界坐标转换成操作相机的局部坐标，统计横向和纵向需要的范围。
3. 正交尺寸表示画面的半高。纵向需求直接使用 `halfHeight`；横向需求除以 `workspaceCamera.aspect`，也转换成所需半高。
4. 两个方向取较大值、乘以余量，再与最小尺寸比较，写回 `orthographicSize`。因此窗口变窄时相机会扩大视野，保持棋盘完整。

核心关系：`正交半高 = max(最小半高, max(纵向需求, 横向需求 / 视口宽高比) × 余量)`。

脚本不读取输入、不移动相机、不改动实验相机。范围暂按当前单层规则设置；后续加入多层时需要扩展 Y 范围。极窄窗口会让棋盘和预览较小，工具界面的自适应布局另行处理。

## 新增的记录和辅助文件

- `docs/research/simulation-workspace-view-github.md`：编码前候选、实际读取的源码和许可证、采用方式及验证结论。
- `.scratch/task-a-simulation/issues/03-workspace-view.md`：本步范围和完成记录。
- `.scratch/task-a-simulation/workspace-view/`：配置前场景备份、实际编辑器状态、运行截图和验证结果，不参与正式运行。
- 临时 `SimulationWorkspaceSetup.cs`：读取内存、按命令配置、备份与保存场景、实际运行采集；完成后移到 `.scratch/task-a-simulation/diagnostics/`。不重新启用旧的 SimulationLiveDiagnostics。
- `.scratch/task-a-simulation/diagnostics/FocusUnityWorkspace.ps1`：只唤醒已存在的当前工程窗口，供导入和核验使用，不启动或关闭 Unity。
- Unity 为新脚本生成的 `.meta` 保存脚本 GUID，场景通过它找到取景脚本；应与脚本一起保留。

## 如何验证与学习

在 Unity 打开正式仿真场景，进入 Play，观察主区域与小预览中的三块积木。检查完整棋盘、顶面/侧面和预览比例。改变 Game 窗口比例，主区域应自动扩大或收缩取景。

在 Play 中暂时调整 WorkspaceCamera 的旋转，观察主画面改变而顶置小预览保持不变；停止 Play 后临时旋转会恢复。固定视图阶段尚没有鼠标旋转与缩放输入。

具体已执行的验证、截图及限制记录在 `.scratch/task-a-simulation/workspace-view/README.md`。下一小步先把操作区域的鼠标位置转换为射线与固定格位，再接入添加/删除/移动及真实状态。
