# 俯视仿真光照：使用与代码说明

更新：2026-10-19。环境：Unity 6000.3.24f1、Built-in、`Brick2LifeSimulation`。本轮重点是固定俯视采集 RGB 的照明变化；不改变已有顶部相机位置、Perspective/FOV 50 和操作相机姿态。

## 在 Unity 中使用

打开 `Assets/Scenes/Brick2LifeSimulation.unity` 并点击 Play。右侧预览标题下的“光照：双侧 · 标准”可展开设置面板，提供左侧、右侧、双侧、低/标准/高亮度、左右比例、环境光倍数、阴影开关和方向光对照。窗口较小时可滚动内容，底部“返回相机预览”固定可见。收起面板后可继续原有积木编辑；面板不进入顶置相机采集的 RGB。

“保存 RGB + 标注”仍输出 `SimulationCaptures/` 内同名 PNG/JSON。切换光照后立即保存即可；采集函数主动渲染当前相机，不必等待预览刷新。

默认双侧、标准、开启阴影，左右比例 **50:50**、环境光 **1.00 倍**，保持此前默认成像。三个档位的合成强度预算分别为 1.5、3、4.5；单侧灯使用全部预算，双侧按比例分配。此处是 Unity intensity，不是实测 lux，也不保证单侧与双侧每个像素同亮。

### 调整左右比例与环境光

1. 选择“双侧”，拖动“左右比例”滑块。范围 0:100～100:0，总直射强度不变。例如标准档总强度 3，75:25 对应左灯 2.25、右灯 0.75。滑块端点保留 Dual 模式和两盏 enabled 灯，其中一盏实际 intensity 为 0。
2. 拖动“环境光”滑块，范围 0～3 倍，立即改变底板与积木的环境填充光。0 表示没有这一项填充光，直射灯仍照明；1 为基础环境光；3 为基础 RGB 的三倍。
3. “恢复比例和环境光”将两条滑块恢复到 50:50、1 倍，保留当前方向、亮度档和阴影选择。单侧及方向光模式下比例滑块禁用，原比例保留，切回双侧后恢复生效。

在 Play 内操作滑块属于本次运行设置；如需更改长期默认值，在退出 Play 后选中 `SimulationLighting`，修改组件中的 `Left Light Share`（0～1）和 `Ambient Multiplier`（0～3），保存场景。标准强度仍由 `Standard Intensity` 控制。Inspector 参数在下次应用配置时生效，运行时滑块会直接调用应用逻辑。

## 灯的位置和环境

| 设置 | 当前数值 |
| --- | --- |
| 左光位置 | 世界坐标 `(-3,12,5)` |
| 右光位置 | 世界坐标 `(13,12,5)` |
| 朝向 | 都指向底板中心 `(5,0,5)` |
| 灯型 | 两个实时白色 Spot；range 40、outer angle 100°、inner angle 85° |
| 阴影 | Soft/None 可切换；强度 0.8，bias/normalBias 0.02，nearPlane 0.1 |
| 环境 | Flat 基础灰色 `(0.12,0.12,0.12)` × 环境倍数；ambientIntensity 保持 1，reflectionIntensity 0 |
| Play 时的质量 | All shadows、High resolution、shadowDistance 60、至少 2 个 pixel lights |

运行时原 `SimulationLight` 方向光在 Spot 模式下禁用；选择“方向光对照”时只启用该方向光。对照沿用原机位方向，但也受当前环境光与阴影开关影响，因此不是旧场景逐像素复原。

Flat 环境模式的实际颜色通过 `RenderSettings.ambientLight` 设置，与 `ambientSkyColor` 同值。这里每次都用原始 `ambientColor.rgb × ambientMultiplier` 重新计算，alpha 固定 1，不对上一次结果连续累乘。Unity 的 `ambientIntensity` 只影响 Skybox 环境，本轮没有把它当作 Flat 强度或相机曝光控制。API 核实见 [本轮研究](research/simulation-light-balance-ambient-github.md)。

这是对侧面有限光源的可控近似。Built-in 的这两盏 Spot 不模拟真实灯管长度、光谱、矩形面光源的半影、相机自动曝光或白平衡。高亮档可出现浅色区域饱和，适合压力测试；默认双侧标准档在本次场景采集中未出现 RGB 三通道同时达到 254 的像素。

## 新文件及方法各做什么

`Assets/Scripts/Simulation/SimulationLightingSettings.cs` 是实验控制器，不负责识别或改变积木颜色标签。

- `SetLayout` / `SetBrightness` / `ToggleShadows`：按钮调用入口，更新选项并应用。
- `SetLeftLightShare` / `SetAmbientMultiplier`：滑块入口；忽略 NaN/Infinity，将有限输入限制在 0～1 或 0～3，再应用到真实灯和环境颜色。
- `ResetBalanceAndAmbient`：一次恢复比例与环境光默认值，并通过 `Changed` 同步界面。
- `ApplyConfiguration`：按 `T×leftLightShare` 与 `T×(1-leftLightShare)` 分配双侧强度；单侧仍为 T。应用 Flat 环境颜色、灯和阴影；只有 Play Mode 才修改全局 QualitySettings，避免安装场景时直接改质量资产。
- `OnEnable` / `OnDisable`：进入实验时保存原有环境、阴影质量和受控灯状态，禁用或退出 Play 时恢复。两盏灯的几何位置由场景保存，不在每帧重新设置。
- `Changed`：通知界面刷新当前选择；没有逐帧轮询或随机光照。

`Assets/Scripts/Simulation/SimulationLightingControls.cs` 把现有 uGUI 字体与按钮模板接到设置上。

- `Start`：建立菜单按钮、方向/亮度/阴影按钮和面板，使用原预览说明位置，避免增加一列常驻控件。
- `MakeButton`：清空模板的持久事件，再绑定本按钮动作，避免克隆到颜色选择逻辑。
- `MakeSlider`：以现有 uGUI 建立可拖动的比例和环境滑块，连接到控制器，无新包。
- `MakeScrollContent`：建立带 RectMask2D 的内容视口和滚动条，返回按钮留在滚动区域外，确保小窗口可收起面板。
- `TogglePanel` / `Refresh`：开关面板、更新标签与选中背景；用 `SetValueWithoutNotify` 同步滑块，避免更新回路。面板 Image 接收 UI 射线，防止点击穿透。
- `OnDisable` / `OnDestroy`：取消事件订阅，隐藏或销毁运行时控件，并恢复原预览说明。

`Assets/Scripts/Simulation/SimulationLightingConfiguration.cs` 是实际光照快照的数据结构。

- `SnapshotScene`：读取场景全部灯，包括额外、禁用和 inactive 灯；读取控制器预设、环境光、反射强度、阴影质量及颜色空间。
- `ObjectPath`：使用场景路径、对象名称和兄弟序号建立稳定顺序，不使用跨运行变化的 InstanceID。重命名或重排灯的层级会改变配置 ID。
- `F` / `C`：拒绝非有限数值，统一数值表示。

`Assets/Editor/SimulationLightingSetup.cs` 提供 `Brick2Life > Simulation > Install lighting controls`。`Install` 查找现有仿真 UI 与方向光，建立/连接控制器和两盏灯，标记场景已修改；`Spot` 配置灯的几何和阴影参数。它不自动保存，不在运行时加载，也不作用于旧塔防演示。现有仿真场景已经安装，无需每次运行菜单。`Assets/Editor/BrickDefense.Editor.asmdef` 新增对 Simulation 程序集的引用，使此编辑器工具能访问组件。

`Tests/Unity/SimulationLightingChecks.cs` 及 `Tests/RunSimulationLightingChecks.ps1` 将当前 Simulation 源码、场景、材质、字体及渲染设置复制到独立工程，运行 Play Mode 实际采集。检查代码不放进正式 Assets，不干扰主工程已打开的 Unity。

## 既有采集代码如何扩展

`SimulationCameraCapture.cs` 的顶层 GT schema 仍为 **3**；此次扩展把嵌套光照 schema 从 1 升为 **2**：

| 字段 | 含义 |
| --- | --- |
| `lightingConfigurationId` | 当前为 `lighting-v2-` 加 SHA-256；与相机配置编号分离 |
| `lightingConfiguration.schemaVersion` | 当前 2，独立于顶层版本 |
| `controllers` | 预设意图：布局、亮度、阴影请求、合成预算与控制器状态 |
| `controllers[].leftLightShare` | 双侧时左灯所占的强度份额，0～1；右侧份额为 1 减该值 |
| `controllers[].ambientMultiplier` | 请求的环境光倍数，0～3 |
| `controllers[].baseAmbientColor` | 倍乘前的基础颜色，用于解释和重放实际环境光 |
| `lights` | 实际灯类型、活动状态、位置/旋转、颜色、强度、范围、角度、cullingMask、阴影模式与参数等 |
| 环境及质量字段 | 实际 ambientMode/颜色/强度、reflectionIntensity、全局阴影质量/距离/分辨率、pixelLightCount、activeColorSpace |

`Snapshot` 在渲染前同时固定布局、相机和光照。`SaveImage` 在 Render 后再次读取光照并比较 ID；如果回调在拍摄中修改了灯、环境光或已记录的阴影参数，拒绝发布这一对文件。配对写入及临时文件清理沿用原逻辑。相机的哈希内容和 `camera-v1-` 编号算法没有改动。

光照 ID 不含时间、帧号和积木布局；相同设置再次拍摄得到同一 ID。它描述本次记录的配置，不是完整渲染状态的证明：没有对所有材质、烘焙数据、反射探针、后处理或 cookie 纹理内容做完整指纹。

旧光照 schema 1 / `lighting-v1-` 样本保持原样。读取器应按嵌套版本解释，新请求字段在旧文件中缺失时，旧双侧语义为 0.5、环境倍数为 1，不能把缺字段默认当作 0。新旧 schema 即使默认 RGB 相同，配置 ID 也不同。请求比例被保留在 ID 内，因此单侧模式下通过 API 改了保留比例，实际 RGB 可以不变而 ID 改变；它不是像素指纹。

与 B/C 的职责仍保持原约定：B 用 RGB 和约定公开的标定信息识别；GT 颜色、占用和编辑动作留作评估依据。光照配置可作为实验诊断字段，不要求 B 依赖 Unity 灯参数才能识别。C 的状态/事件接口不变。读取旧样本时，应容许 schema 1/2 缺少光照字段。

## 验证结果与复现

测试集数量研究见 [数量与覆盖记录](task-a-benchmark-plan.md)。该大规模方案及其采集实现、生成数据已撤回，仅保留研究参考；下面的既有光照检查结果继续有效。

当前扩展检查：**334 项断言通过，34 对实际 PNG/JSON**。检查项目目录：`.scratch/lighting-validation/run-20261019-220238-126/`，汇总为 `verification.json`，包含新比例 0/25/50/75/100%、环境 0/1/3 倍的实际 RGB、单侧不受保留比例影响、重置回到同一配置与相同像素、无效输入、采集中变更拒绝、指针按下/拖动滑块、模式禁用与恢复。

uGUI 使用真实渲染目标生成截图，并检查 1024×768、640×480、571×1000 三种尺寸中滑块、滚动后的恢复按钮和固定返回按钮可点击；对应文件为 `lighting-ui-<宽>x<高>-top/bottom.png`。这包括前序已有的“左侧/双侧 × 三档亮度 × 阴影开关”12 组合、额外灯/环境/质量变化及全局状态恢复检查。

同一组六块积木的固定底板采样说明了新增控制确实改变 RGB：

| 条件 | 左侧底板采样 | 右侧底板采样 |
| --- | --- | --- |
| 比例 25:75，环境 1 倍 | 134 | 183 |
| 比例 50:50，环境 1 倍 | 159 | 159 |
| 比例 75:25，环境 1 倍 | 183 | 134 |
| 比例 50:50，环境 0 倍 | 135 | 135 |
| 比例 50:50，环境 3 倍 | 206 | 206 |

以上是约定点的灰度亮度（0～255），不是照度测量。新增控制的默认值保持此前基线；初版六块积木基线的旧检查记录仍在 `.scratch/lighting-validation/run-20261019-212147-729/`（200 项、21 对）。

本次 512×512 俯视图的整图平均亮度（0～255，含背景，不是照度）：

| 布光（阴影开） | 低 | 标准 | 高 |
| --- | --- | --- | --- |
| 左侧 | 80.93 | 129.28 | 163.06 |
| 双侧 | 81.15 | 130.48 | 177.89 |

四个对称底板采样点在“双侧标准”约为 159/159/159/159，左侧标准约为 208/110/208/110；证明侧光梯度与双侧对称行为。双侧标准无三通道白色饱和，双侧高档约 15.37%、左侧高档约 24.74% 的整图像素三通道达到 254；高档因此视为压力条件，不能当作理想照明。阴影开/关的每组实际 RGB 均存在超过 100 个明显变化像素。

初版主工程验证也已实际进入 Play、打开光照面板并保存六块积木样本：`SimulationCaptures/rgb_20261019_132033551_29cace274cd04f67ab671bb54fc4dc46.png/.json`，旧截图在 `.scratch/lighting-validation/workspace-lighting-menu.png`。

本次扩展在主工程保留运行中的 **10 块积木** 后重新进入 Play，实际打开新滑块面板并保存 `SimulationCaptures/rgb_20261019_141244476_a585b9f2fbe04e0688cb1d46eaabbe1a.png/.json`。核对外层 schema 3、光照 schema 2 / `lighting-v2-`、10 个占用格、Perspective/FOV 50、50:50、环境 1 倍；两个滑块在主工程中实际可见。记录在 `.scratch/light-balance-validation/main-capture.json`、`main-menu-state.json` 与 `main-light-balance-menu.png`。本轮未新增正式运行文件，只扩展现有四个 Simulation 源文件和检查；临时布局备份及编辑器检查工具只用于保留现场与核对。

完成后已保存场景并退出 Play，10 块积木仍保留；临时编辑器工具和 Assets 中的布局备份已移除，场景不引用临时 prefab。布局备份副本留在本地 `.scratch/light-balance-validation/placed-bricks-backup.prefab`，不作为正式资源。

本轮相机回归同样通过 **684 项断言、15 对 PNG/JSON**：`.scratch/camera-metadata-validation/run-20261019-220236-653/verification.json`。测试使用隔离工程，不把运行结果目录加入正式资源。

```powershell
.\Tests\RunSimulationLightingChecks.ps1
.\Tests\RunSimulationCameraMetadataChecks.ps1
```

研究及许可证比较见 [初始光照研究](research/simulation-lighting-controls-github.md) 与 [比例/环境光扩展研究](research/simulation-light-balance-ambient-github.md)。本轮只借鉴归一化控制映射至实际灯参数的机制，未引入新包或复制外部源码。实际硬件、Android 与 B 的识别准确率尚未验收。
