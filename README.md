# BrickDefenseMVP → Brick2Life

**方向与实现状态（2026-10-18 核对）：**当前 `feat/task-a-unity-bricks-camera` 分支已包含 Brick2Life 单层仿真工作台：三维操作主视图、固定顶置相机预览、积木增删与拖拽移动、可配置颜色、清空棋盘，以及 RGB PNG 与真值 JSON 配对导出（包含棋盘四角的世界/图像坐标）。场景为 `Assets/Scenes/Brick2LifeSimulation.unity`，需在 Unity 中单独打开。识别、变化检测、游戏更新和投影反馈的 Brick2Life 闭环尚未接通。

下文主要介绍保留的 BrickDefenseMVP 单张照片塔防原型；Build Settings 的默认构建入口仍为 `Assets/Scenes/BrickDefenseDemo.unity`。当前功能、验证依据和未完成项见 [项目已实现功能汇总](docs/implemented-features-2026-10-18.md)。新方向的范围、文件清单、三人分支和 PR 顺序见 [Brick2Life 开发方案](docs/brick2life-development-plan.md)。

2026-10-19 仿真扩展：Play 后点击右侧“光照：双侧 · 标准”，可选择左侧/右侧/双侧白光、三档亮度、左右强度比例、环境光倍数和阴影开关。默认双侧标准亮度、比例 50:50、环境 1 倍，小窗口可滚动操作。采集 JSON 顶层 schema 3、光照 schema 2 同时记录请求设置与实际生效值；使用方式、代码入口和实测边界见 [光照说明](docs/task-a-lighting-guide.md) 与 [相机元数据说明](docs/task-a-camera-metadata-guide.md)。

旧版源码由 `brickdefense-v1.3.1` 标签固定在提交 `c3bce83`。另有仓库外的 Git 历史备份与 Windows 试玩包；它们不包含尚未提交的实验记录工作。

实体积木塔防项目：单张照片 → 可编辑的 10×10 草稿 → 实物校对并记录两项正确率 → 自由设计游戏布局 → 20×20 游戏格 → Unity 三维地图与波次战斗。也可直接使用模拟示例。照片仅保留无格纸识别，由用户标定背景和颜色角色。通用实拍准确率尚未验收。

## 运行演示

1. 用 **Unity 6000.3.24f1** 打开本目录。
2. 打开 `Assets/Scenes/BrickDefenseDemo.unity`，点击 Play。
3. 点击空白游戏格添加出生点，再点击同一格取消，可以选择多个；用底部按钮切换“需求示例布局”和“核心被包围布局”。

默认出生点是 `(1,18)`。需求示例应显示 9 块积木、36 个占用游戏格、到实体格 `(7,7)` 核心的 16 步路线。核心被包围布局应显示无可达路线的提示（结果代码 `NO_ROUTE`）。柔和蓝绿代表核心、暖石色代表墙、赭黄代表塔，深绿标出路线和有效出生点，砖红标出无效出生点。

## 柔和策略界面

地图采用暖色桌面、鼠尾草绿棋盘与圆角浮动卡片。底部“示例布局”/“封闭核心”切换示例，“扫描积木”打开照片页；右上角显示路线状态、积木数量和出生点数量，下面是“设置战斗”入口与战斗状态卡片。每个出生点有编号，各自预览到最近可达核心的路线。相机根据窗口比例适应棋盘宽度。

地图、战斗参数和结算、照片导入和标定界面均使用简体中文。共享主题使用随工程打包的 Noto Sans SC 字体，完整 OFL 许可证随字体保留；中文错误翻译集中在 `Presentation/ChineseUi.cs`。来源比较见 [中文 UI 检索](docs/research/chinese-ui-github.md)，实际宽/窄屏操作与截图见 [验证记录](.scratch/chinese-ui/verification/README.md)。

### 视角、缩放与手机操作

默认采用 **55°斜俯视的正交桌游视角**，能看到积木顶面和侧面；远处和近处的同尺寸积木显示大小一致。俯仰限制为 45°—75°，旋转与缩放时始终保持正交投影。

| 操作 | 电脑 | 手机 |
| --- | --- | --- |
| 添加 / 取消出生点 | 左键点击空格；再次点击取消 | 单指轻点空格，抬起时确认；再次轻点取消 |
| 旋转与俯仰 | 按住右键拖动 | 单指拖动 |
| 放大、缩小 | 地图上滚动鼠标滚轮 | 双指张开、收拢 |
| 按钮控制 | 点击 | 轻点 |

相机旋转和缩放按钮面板已移除。使用上述鼠标或手机手势操作；缩放范围 **65%—250%**，围绕棋盘中心，旋转和缩放后仍按实际地图位置选格。每次进入 Play 使用默认角度与 **100% 全图**。

拖动和捏合不会误选出生点；从 UI 开始的手势不操作地图，照片页暂停主地图输入，校对的三维预览独立处理旋转和缩放。捏合后需要全部抬起，再开始单指旋转或轻点。保留触摸模拟鼠标供现有按钮和照片标定使用，地图屏蔽重复输入。手机手势已接入当前 Legacy Input；编辑器检查不等于真机验收，本机仍缺少 Android 构建模块。

轨道计算局部改编自 MIT 授权的 JrsOrbitCamera，见 [检索比较](docs/research/orbit-camera-github.md) 及 [第三方声明](THIRD_PARTY_NOTICES.md)。

捏合距离计算继续有限改编同一 MIT 来源；新增手势生命周期与输入隔离见 [手机手势与缩放研究](docs/research/touch-zoom-github.md) 和 [.scratch/touch-zoom/spec.md](.scratch/touch-zoom/spec.md)。

照片页按“照片预览 → 角色颜色 → 核对地图”整理操作，宽屏双列，窄屏单列滚动。地图角色材质与实际积木颜色相互独立；原照片、标定色块、检测框和数字地图仍展示实际颜色。

配色与控件样式集中在 `Assets/Scripts/Presentation/SoftStrategyTheme.cs`，三维材质、光照和底座在 `LevelView.cs`，轨道与缩放在 `BoardOrbitCamera.cs`，手机手势在 `BoardTouchControls.cs`，地图绘制与触摸共用的 UI 区域在 `BoardUiLayout.cs`。GitHub 源码比较与有限借鉴见 [界面研究记录](docs/research/soft-strategy-ui-github.md)，当时的界面验证见 [截图与操作记录](.scratch/soft-strategy-ui/verification/README.md)。

## 游玩：实体布局挑战 A

战斗前可以选择多个空白出生点；再点已选格取消，也可取消默认 `(1,18)`。零出生点时禁用开局；新点被占用或不可达时提示原因并保留原选择。封闭示例的红色默认入口仍可取消。

**每波数量是所有出生点的合计。**敌人按选中顺序轮流生成，跨波继续轮转，各自沿所属入口的四方向最短路移动。例如 3 个入口、每波 8 只，会依次分配 3/3/2 只，下一波从上次轮转的后一个入口继续。所有入口在战斗开始后一起锁定；重试保留入口并从首个入口重新轮转。此功能扩展 v1 的“一个出生点”条款，见 [多出生点规格](docs/requirements-multiple-spawns.md) 和 [GitHub 检索记录](docs/research/multiple-spawns-github.md)。

地图路线有效后，点击右侧“设置战斗”，在参数弹窗中设置波数、每波数量、敌人生命、速度、抵达伤害和出怪间隔，再点“开始第一波”。敌人沿现有四方向 A* 路线移动；塔攻击平面射程内最近的存活敌人，开火时结算伤害，同时显示射击轨迹、命中闪光与血条。点击塔可显示或关闭射程。

第一波开始后固定地图、出生点和参数；波间也保持锁定。所有核心共用生命值，墙只阻挡移动。每波全部出怪并清场后，点击“开始下一波”；最后一波结束且核心存活获胜，核心归零立即失败。“暂停”/“继续战斗”控制战斗时钟，相机仍可旋转和缩放。手机切到后台会暂停，回来后手动恢复。

结算显示守住的波数、击杀、抵达核心的敌人数与核心生命。“重试当前布局”重置本局；“返回布局”返回地图并重新扫描。进行中“结束本局”放弃本局并解锁地图。

**试玩配置，尚未平衡验收：**3 波，每波 8 只，敌人生命 24、速度 1.4 游戏格/秒、核心伤害 10、间隔 1.1 秒。核心初始生命 100；塔伤害 12、射程 8 游戏格、间隔 0.45 秒。塔与核心参数在场景入口 `BrickDefenseDemo` Inspector 设置。输入上限为 20 波、50 只/波、总计 400 只；生命 1—10000、速度 0.1—20、伤害 1—1000、间隔 0.1—10。上限是保守开发配置，不是 Android 实测能力。

实现分为 `Contracts/CombatModels.cs`、`Domain/BattleSimulation.cs`、`Application/BattleSession.cs`、`Presentation/CombatView.cs` 与 `BattlePanel.cs`。规则无 Unity 引用，固定 60 Hz 推进；渲染卡顿时每帧最多推进 0.25 秒，后台不补跑。源码比较与有限适配见 [战斗研究](docs/research/combat-a-github.md)，验收见 [.scratch/combat-a/spec.md](.scratch/combat-a/spec.md)。

## Windows 可运行版

本地试玩包：`Builds/BrickDefense-v1.3.1-Windows-x64.zip`。解压整个目录后双击 `BrickDefense.exe`，无需 Unity 或 .NET SDK。包内包含中文使用说明、实拍示例和第三方许可证；Windows 点击“导入照片”直接弹出系统选图窗口，支持 PNG/JPG/JPEG，取消会保留当前照片及识别结果。

v1.3.0 已完成实际程序的“导入实拍→颜色采样→地图校对→保存指标→生成关卡→单波战斗→胜利结算”检查；v1.3.1 修复 Windows 缺少选图窗口的问题，并在真实 Player 验证弹窗、三种图片格式、中文/空格路径和取消保留结果。构建说明见 [v1.3.0 版本报告](docs/releases/v1.3.0-windows.md)，修复与当前包见 [v1.3.1 版本报告](docs/releases/v1.3.1-windows.md)。构建产物不进入 Git。

## 使用真实照片

照片识别仅使用无格纸流程，格纸模式、切换按钮及格纸导出已删除。当前拍摄与接口规范见 [v1.2](docs/requirements-plain-only-v1.2.md)，检索与有限复用见 [裁剪记录](docs/research/plain-only-photo-github.md)。

### 拍摄与标定

使用干净、能与积木区分的背景；同尺寸立方体、单层，尽量从正上方拍摄。全部积木完整入镜，并至少保留一块清晰独立方块用于估计尺寸。不要裁切边缘积木。

1. 点击“扫描积木”→“导入照片”，在系统窗口中选择完整 PNG/JPG/JPEG；也可以“启动相机”→“拍照”。Unity 编辑器及 Windows 桌面版均提供选图窗口。Android 拍摄已接入权限申请，尚未真机验收，相册选择器尚未接入。
2. 点击“颜色设置”→“采样背景”，选择干净背景处；再分别从核心、城墙、防御塔的积木顶面取色，避开阴影和反光。颜色与角色由你决定。
3. 点击“保存颜色”→“分析照片”。已有无格纸标定会自动恢复，同一套积木和光照下可以直接继续使用；更换积木或光照明显改变时重新标定。
4. 点击“进入地图校对”。对照原照片，在 10×10 草稿中选择积木再点空格移动；“添加/改色”配合角色按钮补加漏检或修改颜色；“删除”移除误检。列表可以选中重叠或棋盘外的积木。支持撤销、恢复识别结果和整体按格平移。
5. 勾选“我已逐块对照照片确认数量、颜色和位置”，点击“确认实物 · 记录准确率”。无需道路或核心即可确认实物真值；数量、颜色和格位由玩家核验。
6. 进入游戏布局设计，继续增删、改色、移动积木；这些设计操作不改变已确认的准确率。“三维预览”可在缺核心/无路时浏览草稿，沿用鼠标和触摸旋转缩放；返回编辑网格继续修改。
7. 设计布局包含核心且没有重叠/越界后，点击“生成关卡”，返回地图选择出生点并设置战斗。可达性由出生点与战斗入口检查。未发布的草稿和失败处理不会覆盖现有地图；返回照片后重新进入可继续同一份草稿，换图或重新分析会建立新草稿。

照片中以红框标出能定位的裁切、未知颜色、几何或拆分问题。原始分析仍记录拒绝状态及原因，同时保留已检测出的积木供人工校对；用户可以补加遗漏或重新拍摄。没有可用检测的分析也可手工补建草稿，确认前不视为自动识别成功。

### 相对布局与适用范围

结果按照片左上方向，以检测到的方块边长估计间距，最上/最左的方块放在相对行/列 1，保留边缘空位。空白格和间距为离散近似，没有固定物理原点、真实毫米尺度或从图像恢复的堆叠高度。报告记录 `coordinateBasis=RelativeBrickScale`、`boardIsReliable=false`。

标定沿用 `BrickDefense.PhotoCalibration.plain.v1` 四颜色数组；原格纸色样不会替代背景色。实拍回归样本 `Tests/Fixtures/real-unmarked-bricks-v1.jpg` 保持原文件不变，共 7 橙、2 蓝、1 白、10 块和 90 个相对空格；角色由标定决定。

`real-cropped-bricks-v1.jpg` 对应曾导入的裁切照片，原始分析继续记录拒绝，允许保留可用检测进入人工校对。拍摄仍建议使用完整原图。严重斜拍、明显旋转、堆叠、遮挡、复杂背景或缺少可估尺寸的独立方块尚不属于当前自动识别支持范围。

### 输入与扫描记录

支持 PNG/JPG，文件不超过 32 MB、长边不超过 4096 像素。识别在本地完成；原图、方向归一后的照片、颜色样本、格位/积木/像素证据、错误与耗时保存在 `Application.persistentDataPath/Scans/<scanId>/`，输入类型固定记录为 `PlainBackground`。

同目录新增 `review-<reviewId>.json`，保存原始积木、人工确认真值、游戏设计布局、编辑操作、两项正确率和发布状态。原 `result.json` 不被修改。识别正确率只比较检测与颜色；格位恢复正确率比较行列，分母均为实物真值块数，补加块在两项指标中均计为原始错误，误检另报。位置按预先固定的相对坐标约定（各布局最左列/最上行归一到 1）比较，整体平移不改变指标。自由设计阶段的数据独立于真值；需要修订真值时点击“返回实物校对”并重新确认。准确率取决于玩家对实物的核验，不代表独立测试集验收。

新增流程及验收规则见 [地图校对 v1.3](docs/requirements-layout-review-v1.3.md)，候选源码与有限借鉴见 [研究记录](docs/research/layout-review-github.md)。

开发参数在 `Assets/Resources/photo-scan-settings.json`。使用既有 Colourful Lab 适配比较颜色，低饱和背景与积木额外保留明度区分；同色组件按尺寸和实际颜色覆盖拆分。置信字段表示覆盖比例，不是经过实测校准的正确概率。通用实拍准确率仍需更多标注样本验收。

Unity 菜单 `Brick Defense/Verify Photo Integration` 可检查原 JPG 解码、PNG 往返与关卡生成；早期格纸/AI 图片及实验文档保留为历史资料。

## 逻辑检查

安装 .NET 10 SDK 后，在项目根目录运行：

```powershell
dotnet run --project Tests/BrickDefense.Checks.csproj
```

检查覆盖示例数量、2×2 格映射、四方向最短路线、等距核心的固定选择、封闭核心、被占用出生点、同色相邻积木的独立编号，以及不确定格位、未映射颜色、畸形网格和缺少核心的拒绝。另用独立 BFS 对照 200 个固定随机种子的布局，核对最短距离、目标选择和路径通行性。

照片检查读取原实拍 JPG 的 RGB 数据并核对人工标注的 10 个对象，覆盖缩放、用户互换角色、L 形/2×2 同色紧贴、未知色、裁切、空背景、未标定、非法设置和旧结果失效。Unity 菜单 `Brick Defense/Verify Photo Integration` 验证 JPG 解码、PNG 往返与关卡映射。

流程检查覆盖照片与两个示例反复切换、非法输入保留当前关卡、输入数据快照、出生点变更，以及确认时使用分析当时的标定规则。

多出生点检查覆盖增删/清空、无效新点保持选择、各入口独立核心和路线、合计数量与跨波轮转、共享生命、战斗锁定/快照/重试及第二条路线校验。Unity 多入口战斗和相机/触摸回归均通过，截图与限制见 [.scratch/multiple-spawns/verification/README.md](.scratch/multiple-spawns/verification/README.md)。
Unity Play 回归通过公开关卡接口验证实际 Renderer 和相机渲染，并检查程序集边界、原 JPG 导入、标定读写及扫描报告保存。

无格纸 Unity 操作回归验证默认加载已有标定、真实取色、保存、10 块识别、确认、报告相对坐标、裁切拒绝保留地图、照片原色和宽窄屏按钮。截图与结果见 [.scratch/plain-only-photo/verification/README.md](.scratch/plain-only-photo/verification/README.md)。

拒绝回归额外覆盖用户原标定、灰背景明暗渐变、真正裁切 JPG、拒绝区域原图坐标和当前关卡保留。`--scan-rejection-checks` 可单独运行。Unity 场景回归还验证拒绝区域 JSON 保存；隔离项目的 Game 视图截图验证面板遮挡，详见 `.scratch/scan-rejection/spec.md`。

## 代码位置

整体数据流程、模块职责与程序集依赖见 [架构说明](docs/architecture.md)。五个主要模块已建立独立 `.asmdef`；Domain、Vision、Application 以及公共数据和算法程序集不引用 Unity API。

- `Assets/Scripts/Contracts/`：共享布局、规则、格位、结果和照片证据数据。
- `Assets/Scripts/Domain/`：纯 C# 的模拟扫描、格位映射、关卡校验和 A* 寻路。
- `Assets/Scripts/Vision/`：纯 C# 的照片数据、颜色标定、积木检测与相对布局。
- `Assets/Scripts/Vision/PlainBackgroundRecognizer.cs`：无标记背景分割、同色组件拆分与相对布局。
- `Assets/Scripts/Application/`：`LevelSession` 集中管理关卡上下文；`PhotoScanSession` 管理分析状态和确认规则。
- `Assets/Scripts/Presentation/`：场景接线、三维绘制、示例交互和照片界面。
- `Assets/Scripts/Infrastructure/`：图片读取、相机、Unity 像素转换、标定与扫描报告持久化。
- `Assets/ThirdParty/RoyTAStar/`：固定版本的 Roy-T.AStar 最小堆与完整许可证。
- `Assets/ThirdParty/Colourful/`：局部改编的 sRGB/D65 Lab 转换与完整 MIT 授权。
- `Assets/Editor/`：Editor 专用的场景生成、文件选择器和回归工具；重建场景菜单为 `Brick Defense/Create Demo Scene`。
- `docs/requirements-v1.md`：首版历史规格；当前照片入口以 `docs/requirements-plain-only-v1.2.md` 为准。

模拟扫描中的 `demo-core`、`demo-wall`、`demo-tower` 只是示例标识，不能当作实物颜色标定结果。照片识别报告独立的 `physical-color-*` 颜色编号，再由保存的配置映射为角色。

## GitHub 借鉴依据

本里程碑的检索、候选比较和有限整合方案见 [研究记录](docs/research/existing-milestone-github.md)。直接采用的源码范围与授权见 [第三方声明](THIRD_PARTY_NOTICES.md)。功能开发须先按 [项目流程](docs/agents/github-reuse.md) 完成检索记录。

照片识别的候选比较、源码选择和验证依据见 [照片识别研究记录](docs/research/photo-recognition-github.md)。

颜色识别改进的前置检索和实际适配见 [颜色识别研究记录](docs/research/color-recognition-github.md)。

示例在照片关卡之后只显示底板的问题已修复：切换到任一模拟布局时恢复示例的配套颜色规则。
Play Mode 回归覆盖初始示例、照片、两个示例按钮的加载入口、重复切换、实际 Renderer 与相机渲染。
普通示例显示 9 块和 16 步路线；包围示例仍显示 5 块布局，并以 `NO_ROUTE` 提示不可达。
前置比较及复现依据见 [示例切换研究记录](docs/research/demo-switching-github.md)。

五模块重构已完成。`BrickDefenseDemo` 负责场景接线，`LevelView` 负责绘制，`DemoUI` 负责操作；布局、颜色规则与计算结果经 `LevelSession` 整体替换。
照片面板的文件读取、相机和存储职责已移入 Infrastructure。标定保存键、报告格式与场景脚本 GUID 保留。
GitHub 候选比较和有限适配见 [架构重构研究记录](docs/research/architecture-refactor-github.md)。

无格纸实拍的前置检索与有限借鉴见 [无标记实拍研究](docs/research/markerless-photo-github.md)，输入与坐标扩展规范见 [v1.1 扩展](docs/requirements-markerless-v1.1.md)。

实拍拒绝与面板遮挡修复的源码比较、有限借鉴和验证见 [拒绝修复研究](docs/research/scan-rejection-github.md)。
