# Unity 积木仿真平台开发交接摘要

整理日期：2026-10-16，Asia/Shanghai。用于附到新对话，接续现有工程的分步教学与开发。本文已核对当前磁盘文件和 Git 状态；运行画面的验证证据来自 2026-10-15，不代表此刻 Unity 内存状态。

## 1 项目目标与用户负责范围

项目从旧 BrickDefenseMVP 塔防原型转向 Brick2Life 虚拟仿真研究平台。用户负责任务 A：Unity 积木仿真与虚拟相机。

完整系统的后续流程为：

```text
玩家搭建虚拟积木 → 固定相机输出 RGB → 任务 B 识别布局与变化
→ 任务 C 更新游戏内容 → 虚拟投影作用于积木和桌面表面
```

任务 A 应提供：可编辑的积木场景、固定实验相机、RGB 图像与对应的 Ground Truth（真实积木状态）。当前阶段全部在 Unity 中仿真。

任务书来源：[fengong.docx](D:/桌面/fengong.docx)。后续用户的阶段调整优先于任务书和旧原型需求文档；不要按旧的实体照片塔防需求重新扩张任务。

## 2 用户的协作与教学要求

- 用户希望在现有原型条件下，从头一步步完成并学习；每次做一个可理解、可验证的小步骤。
- 代理处理重复劳动，例如材质复制、对象配置、重复代码；需要的 Unity 包已获准按需安装。
- 涉及实质设计选择时，解释区别并让用户决定。已经确认的选择不要反复询问。
- 每次新增文件，说明路径、用途、具体内容、与其他文件的关系和验证方法；新增代码需要分段解释变量、方法、执行过程及可调整参数。
- 用户希望代理直接查看工程和 Unity，已明确表示“自行查看，你有这个工具和权限”。可自行检查的问题，不应反复要求用户截图、查看 Hierarchy 或抄参数。
- 磁盘场景与当前 Unity 内存可能不一致。报告实际运行通过时，应有相应运行证据，不能仅凭场景 YAML 或另一个测试工程得出用户当前画面正常的结论。

## 3 最新阶段决定

以下是本对话最后确认的实施顺序：

| 阶段 | 内容与验收 |
| --- | --- |
| 单层拍摄与识别 | 先完成三维工作台、单层鼠标编辑、固定 RGB 采集和配对真值；任务 B 接入图像恢复格位与颜色；投影关闭 |
| 独立投影验证 | 任务 C 先用人工指定目标或测试数据，验证投影是否作用于积木和桌面的可见表面，以及格位对齐 |
| 单层闭环 | 用视觉预测状态驱动游戏和投影更新，分别测试投影开关及响应延迟 |
| 多层扩展 | 再加入堆叠、层高、遮挡、历史状态维护和完整逐层评价 |

独立投影测试通过不等于识别已经接入。单层是摆放约束，主操作界面仍然是三维工作台。

### 已确定的搭建规则

- 10×10 固定底面网格，单位格边长 1。
- 世界坐标原点位于棋盘最小 X/Z 角落；棋盘表面 Y=0，覆盖 X/Z 的 0～10。
- 逻辑 gridX/gridY 对应 Unity 世界 X/Z；Unity 世界 Y 表示高度。
- 第一阶段每格最多一块单位立方体，layer=0；格号从 0 到 9。
- 数据预留 layer 层号。单位立方体中心的位置关系为 `(gridX+0.5, layer+0.5, gridY+0.5)`。
- 后续多层采用已确认的规则：整格对齐、下方有支撑，只删除或移动每列最上层积木。最大层数尚未约定。
- 至少三种颜色，后续可扩展；颜色暂不绑定核心、墙、塔等游戏角色。
- 材质使用 Standard 并配灯光。棋盘使用整体底板＋LineRenderer 网格线。
- 鼠标使用“添加／删除／移动”工具模式；移动采用拖拽还是两次点击，尚未选择。

### 已确定的视图与采集规则

- 用户明确要求像现实桌面搭积木的三维操作界面，能看到顶面和侧面。
- 计划新增独立 WorkspaceCamera 显示三维操作主视图；现有顶置预览缩到旁边。
- 操作相机用于玩家观察和点选，不作为识别输入；玩家调整观察角度不能带动固定实验相机。
- 单层基线沿用现有顶置正交 TopCamera；单视角指同一台固定实验相机连续拍摄，允许任务 B 利用历史图像。
- 操作相机采用透视还是正交斜俯视尚未选择。用户在旧原型中曾偏好无近大远小的桌游视角，这只是偏好参考，不能当成此次已确认的最终选择。
- 多层实验相机的朝向暂未确定；用户要求先查论文，调研已完成，之后通过对照实验选择。

## 4 工程位置与 Git 状态

- 当前工作区父目录：`D:/桌面/计算机图形学`。
- 实际 Unity 工程与 Git 仓库：[BrickDefenseMVP](D:/桌面/计算机图形学/BrickDefenseMVP)。
- Unity：6000.3.24f1，Built-in 渲染管线。
- Unity 可执行文件：`D:/Unity/Editor/6000.3.24f1/Editor/Unity.exe`。
- 当前分支：`feat/task-a-unity-bricks-camera`，跟踪同名 origin 分支。
- 当前 HEAD：`c1d2d53`，合并任务为 `docs/brick2life-transition`。
- 当前仿真场景、脚本、材质、纹理和研究记录等多数为未跟踪文件；Packages 和 THIRD_PARTY_NOTICES.md 有未提交修改。这轮仿真搭建尚未提交或推送，不能只看 HEAD 判断内容。
- 旧的自动生成完整任务 A 工作树已在先前按用户要求移除，不要重新创建或引用其中旧代码。
- `D:/桌面/计算机图形学/work/experiment-records` 是较早实验记录功能的独立工作树，与当前搭建任务不同。

继续工作时先核对 Git 状态，保留现有未提交修改及 Unity `.meta` 文件；用户没有要求此次提交或推送。

## 5 当前实际完成内容

独立场景：[Brick2LifeSimulation.unity](D:/桌面/计算机图形学/BrickDefenseMVP/Assets/Scenes/Brick2LifeSimulation.unity)。现有原型演示场景仍为 `D:/桌面/计算机图形学/BrickDefenseMVP/Assets/Scenes/BrickDefenseDemo.unity`。

当前仿真场景主要结构：

```text
SimulationRoot             位置和旋转为 0，缩放为 1
├── Board                  Plane，位置(5,0,5)，缩放 1，有 MeshCollider
├── GridLines
│   └── LineX_01            编辑状态下的 LineRenderer 模板
├── BricksRoot
│   ├── RedBrick            中心(.5,.5,.5)
│   ├── GreenBrick          中心(2.5,.5,.5)
│   └── BlueBrick           中心(4.5,.5,.5)
└── TopCamera

SimulationLight
SimulationCanvas
├── PreviewBackground
└── PreviewArea
    └── CameraPreview
```

三个示例积木均为单位 Cube，带 BoxCollider。现在还不是可编辑积木系统。

### Simulation 模块

当前仅有一个自写运行脚本：[SimulationGridView.cs](D:/桌面/计算机图形学/BrickDefenseMVP/Assets/Scripts/Simulation/SimulationGridView.cs)。

- 命名空间：`Brick2Life.Simulation`。
- `lineTemplate` 引用 LineRenderer 模板；`lineWidth` 默认 0.04。
- `Start()` 检查模板，先统一线宽，再循环 0～10，生成 11 条 X 方向线和 11 条 Z 方向线，最后隐藏模板。
- 线位于 Y=0.01。停止 Play 后运行时克隆消失，只剩模板，这是当前预期行为。
- 曾因误引入 `System.Diagnostics` 导致 Debug 名称冲突，已删除该 using。

程序集：[Brick2Life.Simulation.asmdef](D:/桌面/计算机图形学/BrickDefenseMVP/Assets/Scripts/Simulation/Brick2Life.Simulation.asmdef)，当前内容只有程序集名，没有显式引用其他业务模块或 UI 程序集。后续添加依赖前检查实际需要。

### 材质、灯光与相机

- 材质目录：[Assets/Materials/Simulation](D:/桌面/计算机图形学/BrickDefenseMVP/Assets/Materials/Simulation)。
- 网格：`GridLineMaterial.mat`，Unlit/Color，颜色 #404040。
- 积木：`Brick_Red.mat` #E04040、`Brick_Green.mat` #40E040、`Brick_Blue.mat` #4040E0，Standard，Metallic=0、Smoothness=0.2。
- SimulationLight 为白色 Directional Light，Intensity=1，旋转约(50,-30,0)；上次配置未开启阴影。
- TopCamera：位置(5,12,5)，旋转(90,0,0)，正交，Size=5.5，Near=0.3、Far=50，输出到 TopCameraRGB。
- TopCamera 当前带 MainCamera Tag 和 AudioListener。新增操作相机时需明确相机引用与监听归属，避免混用 Camera.main 或出现重复监听。
- [TopCameraRGB.renderTexture](D:/桌面/计算机图形学/BrickDefenseMVP/Assets/RenderTextures/TopCameraRGB.renderTexture)：512×512，当前保存为 R8G8B8A8_UNorm，AA=None，Bilinear，无 mipmap。

### 当前 UI 与显示链路

已启用官方内置 `com.unity.ugui` 2.0.0；manifest、lock 和第三方声明已更新。

```text
三维场景 → TopCamera → TopCameraRGB → CameraPreview.RawImage → Game 窗口
```

- SimulationCanvas 是场景 GameObject，不是目录，也没有同名自写脚本。配置保存在场景文件中。
- Canvas 使用 Screen Space - Overlay；界面不会进入实验相机的 RenderTexture。
- CanvasScaler：Scale With Screen Size，参考 1024×768，Match=0.5。
- GraphicRaycaster 已有；只读预览阶段没有添加 EventSystem。
- PreviewBackground 是铺满屏幕的深色 Image；新增三维主视图时要调整，否则背景会遮住相机画面。
- PreviewArea 锚点为(0.02,0.02)～(0.98,0.98)，提供四周留白。
- CameraPreview 的 RawImage 引用 TopCameraRGB，Color=白色、UV=(0,0,1,1)、Raycast Target 关闭。
- AspectRatioFitter：Fit In Parent，Aspect Ratio=1，使预览保持正方形。
- 当前预览还占据主要画面。WorkspaceCamera、侧边工具面板及预览缩小的方案均尚未实现。

## 6 尚未实现的内容

- 三维操作主视图与操作相机控制。
- 添加／删除／移动及颜色选择工具。
- 独立的真实状态模型与编辑同步。
- RGB 与 GT 的同一时刻快照配对、导出及连续采集调度。
- 新任务的图像识别、变化检测和投影闭环。
- 多层堆叠编辑及单视角层高推断。

旧原型存在 Vision、Domain、Contracts、Application、Infrastructure、Presentation 等模块及先前识别/塔防功能，但不能据此宣称当前仿真新流程已经接通。需要实际检查接口后有限复用，不重新开发完整塔防。

## 7 数据与模块边界

- Simulation 管理真实棋盘、积木编辑、实验相机及真值；Vision 从 RGB 恢复预测状态。
- 共享数据接口放入合适的共享模块，由团队统一；不要让识别、游戏直接依赖 Simulation 内部对象层级。
- 识别输入不得包含 Unity 真值坐标、编辑点击事件或物体 ID 来替代视觉判断。
- RGB、GT、预测与时间记录应按 frameId、时间和布局版本关联，避免拍摄和读取真值之间发生编辑造成错配。
- 投影可使用真实场景几何产生表面和遮挡效果，但投影不能改写积木原始颜色真值。未来游戏/投影的语义反馈由视觉预测驱动。
- 多层阶段区分当前可见观测、历史维持与未知；没有视觉证据不等于积木不存在。
- 严格竖直正交拍摄中，同格同色叠高可能没有可辨别的图像变化；时间历史不能保证弥补完全不可观测的变化。

## 8 已解决问题与验证证据

### 网格线消失

曾存在非正方形纹理、线宽 0.02、纹理预览缩小及运行时参数未同步等因素。最终统一 512×512，并由脚本在克隆前明确设置线宽 0.04。独立 Unity 渲染测试覆盖了 22 条线和旧宽度回归情况。

### Game 显示 No cameras rendering

磁盘场景已有 UI，但用户正在打开的 Unity 内存场景中没有 SimulationCanvas；TopCamera 又输出到纹理，所以屏幕缺少显示入口。

代理曾通过临时 Editor 诊断脚本直接查看主编辑器、补入并保存 UI，在主编辑器实际进入 Play 验证通过：982×730，RGB 积木像素计数 4034、4094、4157。

证据：

- [实际主编辑器运行截图](D:/桌面/计算机图形学/BrickDefenseMVP/.scratch/task-a-simulation/live-game-running.png)。
- [验证结果](D:/桌面/计算机图形学/BrickDefenseMVP/.scratch/task-a-simulation/live-preview-verification.txt)。
- 当时的内存快照：`D:/桌面/计算机图形学/BrickDefenseMVP/.scratch/task-a-simulation/live-editor-state.json`。它是历史快照，不是实时状态。
- 临时诊断脚本已移出 Assets，归档在 `D:/桌面/计算机图形学/BrickDefenseMVP/.scratch/task-a-simulation/diagnostics/SimulationLiveDiagnostics.cs`；不要直接重新启用，它含修复场景和进出 Play 的动作。
- `D:/桌面/计算机图形学/BrickDefenseMVP/.scratch/grid-render-probe` 是独立诊断工程，不是正式功能模块。其测试曾有 Unity Editor SearchDatabase 的非 C# 编译异常，但图像验证通过；不要误报全部日志无异常。

没有现成 Unity MCP 接口。此前直接查看编辑器的办法是临时 Editor 诊断脚本写出实际状态，必要时通过 Windows 恢复现有 Unity 窗口触发导入。桌面窗口访问曾需要命令执行工具的提权；新对话应以实际可用工具和权限为准。不要关闭用户编辑器，也不要用第二个 Unity 实例打开同一个主工程。

## 9 已有记录与编码规则

优先阅读：

1. [AGENTS.md](D:/桌面/计算机图形学/BrickDefenseMVP/AGENTS.md)。
2. [当前任务规格](D:/桌面/计算机图形学/BrickDefenseMVP/.scratch/task-a-simulation/spec.md)。
3. [鼠标编辑任务](D:/桌面/计算机图形学/BrickDefenseMVP/.scratch/task-a-simulation/issues/02-mouse-editing.md)。
4. [相机预览实施记录](D:/桌面/计算机图形学/BrickDefenseMVP/.scratch/task-a-simulation/issues/01-camera-preview.md)。

GitHub 复用规则：新增、扩展或重写功能前，必须检索同类项目，实际阅读 README、相关源码和许可证；候选充足时比较至少两个，将有限借鉴方案写入 `D:/桌面/计算机图形学/BrickDefenseMVP/docs/research` 后再编码。直接使用外部代码需遵守授权并更新 `D:/桌面/计算机图形学/BrickDefenseMVP/THIRD_PARTY_NOTICES.md`。论文调研不能代替功能编码前的 GitHub 检索。

已有研究：

- [网格绘线](D:/桌面/计算机图形学/BrickDefenseMVP/docs/research/simulation-grid-lines-github.md)：参考模板克隆和坐标绘线机制。
- [相机预览](D:/桌面/计算机图形学/BrickDefenseMVP/docs/research/simulation-camera-preview-github.md)：官方 uGUI/RawImage 直接依赖，与 FFmpegOut 比较。
- [单视角多层论文](D:/桌面/计算机图形学/BrickDefenseMVP/docs/research/single-view-stacked-bricks-papers.md)：七篇论文、传感器条件、历史信息与遮挡处理、相机对照实验建议。其开头已注明初期改做单层。

论文要点：InstructioNet（ECCV 2024）使用固定相机与拆解图像记忆，但允许主动显露隐藏结构；MEPNet（ECCV 2022）利用前一结构、部件形状和连接约束，但输入为说明书图像，部分评估使用真值基础结构；DuploTrack/OASIS 依赖 RGB-D，MobileBrick 是移动多视角；这些不能当成固定纯 RGB 可无条件恢复所有层的证明。Single View Metrology 提供几何计高依据，仍依赖可用特征和参考信息。具体来源与章节见研究文件。

本地任务使用 `.scratch` Markdown，状态仅使用 needs-triage、needs-info、ready-for-agent、ready-for-human、wontfix。`.scratch` 保存任务、诊断和验证资料，不参与正式场景运行。相关规则位于 `D:/桌面/计算机图形学/BrickDefenseMVP/docs/agents`。

## 10 下一小步与新对话起点

下一小步：新增三维操作相机，将 Game 主区域改成能看到积木顶面和侧面的三维工作台；保留固定 TopCamera，把现有 RGB 预览缩到旁边。先确认视图，再做单层鼠标编辑。

开始前先核对当前工作树和场景；对操作相机投影方式解释差异并取得用户选择，不要与实验相机配置混淆。对应新功能完成 GitHub 比较记录后再编码。此轮只完成这一小步，不自动做完整识别、多层或投影系统。

之后依次推进：单层编辑 → 独立真实状态 → 单次 RGB/GT 配对导出 → 连续采集 → 与任务 B/C 联调。

可在新对话发送：

> 请先阅读这份交接摘要和当前任务规格，核对现有工程。继续带我分步完成任务 A，下一小步是三维操作主视图与顶置小预览。有设计选择先解释让我判断；重复配置由你处理；新增文件和代码需要详细讲解。
