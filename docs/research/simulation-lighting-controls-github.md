# 仿真光照控制与采集记录：GitHub 检索及有限借鉴

日期：2026-10-19。目标环境：Unity 6000.3.24f1、Built-in、10×10 单层彩色积木、固定顶部 Perspective 摄像机。

## 本次范围与验收依据

用户授权实现：单侧/双侧白光、三档亮度、阴影开关，以及随既有 PNG＋GT JSON 保存光照配置。保持顶部相机与鼠标编辑流程为重点，不迁移渲染管线，不将本轮扩展成真实灯管光谱或物理精确面积光源模拟。背景与第一方照明资料见 [simulation-lighting-layouts.md](simulation-lighting-layouts.md)。旧 `docs/requirements-v1.md` 是历史实体照片需求，本次以用户明确的任务 A 仿真范围为准。

验收目标：同一布局可切换单侧/双侧与三档亮度，阴影开启后能观察积木遮挡；采集文件记录实际灯光、环境光和阴影条件，拥有独立于相机配置的稳定光照配置 ID；渲染期间已记录的光照参数改变时拒绝发布混合快照。三档为可配置合成强度，不冒充实测照度或最佳布光。

本轮沿用已读的 `AGENTS.md`、`docs/agents/github-reuse.md`、`docs/agents/domain.md` 与当前仿真研究约定。只编写本研究文件，未改动代码或场景。

## 检索与实际阅读的两个候选

关键词：`UnityCsReference Light.bindings spotAngle shadowBias`、`UnityCsReference RenderSettings QualitySettings ambientLight`、`Unity Perception LightRandomizer Tag Phase2`。GitHub main/master 是检索当日状态，不声称对应项目安装版本或作过现代平台兼容测试。

| 候选 | 实际阅读内容 | 兼容、依赖、授权和选择 |
| --- | --- | --- |
| [Unity-Technologies/UnityCsReference](https://github.com/Unity-Technologies/UnityCsReference) | [README](https://github.com/Unity-Technologies/UnityCsReference/blob/master/README.md)、[Light.bindings.cs](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Graphics/Light.bindings.cs)、[GraphicsManagers.bindings.cs](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Graphics/GraphicsManagers.bindings.cs)、[LICENSE](https://github.com/Unity-Technologies/UnityCsReference/blob/master/LICENSE.md) 与其链接的 [Reference-Only License](https://unity.com/legal/licenses/unity-reference-only-license)。实际阅读 Light 的 shadows、shadowStrength、shadowResolution、renderMode、spotAngle、range、cullingMask、bias；RenderSettings 的 ambientMode/ambientLight/ambientIntensity/reflectionIntensity；QualitySettings 的 shadows、shadowResolution、shadowDistance、pixelLightCount。 | README 当前标注 Unity 7000，因此只参考公开 API 组织，以 Unity 6.3 文档与编辑器编译验证本地兼容。Reference-Only 不允许改写后分发参考源码；本次只调用公开 API、自写控制逻辑，不复制实现。无新运行包、无需联网，Android 兼容尚未实测。 |
| [Unity-Technologies/com.unity.perception](https://github.com/Unity-Technologies/com.unity.perception) | [README](https://github.com/Unity-Technologies/com.unity.perception/blob/main/README.md)、[LICENSE Apache-2.0](https://github.com/Unity-Technologies/com.unity.perception/blob/main/LICENSE.md)、[Phase2.md](https://github.com/Unity-Technologies/com.unity.perception/blob/main/com.unity.perception/Documentation~/Tutorial/Phase2.md) 内 `MyLightRandomizer`、`MyLightRandomizerTag` 的完整 C# 实现与双灯示例、[RandomizerLibrary.md](https://github.com/Unity-Technologies/com.unity.perception/blob/main/com.unity.perception/Documentation~/Randomization/RandomizerLibrary.md)、[PerceptionCamera.cs](https://github.com/Unity-Technologies/com.unity.perception/blob/main/com.unity.perception/Runtime/GroundTruth/PerceptionCamera.cs) 的 `RegisterNewCapture` 与按帧号配对 RGB 回读机制，以及 [package.json](https://github.com/Unity-Technologies/com.unity.perception/blob/main/com.unity.perception/package.json)。 | Apache-2.0；README 声明已停止官方维护，教程面向 HDRP 与较旧 Unity。整包增加 Burst、Collections、Newtonsoft JSON、render-pipelines.core 依赖，与本次 Built-in 字段/控件扩展不匹配。不装包、不复制代码，仅借鉴逐灯配置与采集快照机制。 |

检索中尝试的库级 LightRandomizer.cs 猜测 raw 路径未能取得正文，因此未将它们列为“已读源码”。实际读过的是 Phase2 的教程实现；它足够说明“控制器枚举灯对象→逐灯应用配置”的机制，但本项目采用固定预设按钮，不引入随机化框架。

Phase2 的两灯示例使用 Directional、Y 旋转约 ±60°、分别配置 0.5～3 与 0～0.4 强度范围。这是教程实例，不是本项目最优参数。当前选用 Spot 是为获得有限位置和范围的可控近似，不照搬该教程灯型或随机颜色。

## Unity 6.3 的必要核实

- [Light.spotAngle](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Light-spotAngle.html) 为聚光锥角度，[Light.range](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Light-range.html) 控制局部光照范围。应验证整个底板及单位积木顶面都在有效覆盖内，不能只看棋盘中心亮不亮。
- [Light.shadows](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Light-shadows.html) 与 [QualitySettings.shadows](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/QualitySettings-shadows.html) 同时影响是否出现阴影。单个 Light 选择 Soft 不能代替全局质量设置，也不能代替 Renderer 的投射/接收阴影和材质支持。
- [RenderSettings.ambientLight](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RenderSettings-ambientLight.html) 用于 Flat 环境照明。为使实验可重复，应显式选 Flat 并固定弱白/灰环境光，而不是只修改一个在当前环境模式下未生效的数值；记录实际模式与相关颜色/强度。
- [Built-in Light Inspector](https://docs.unity3d.com/6000.3/Documentation/Manual/class-Light.html) 提供实时阴影强度、分辨率、bias 等配置；[Types of Light](https://docs.unity3d.com/6000.3/Documentation/Manual/Lighting.html) 明确 Built-in Area 仅烘焙。Soft Spot 是点源锥形照明加阴影近似，不是连续矩形面光源或真实灯管。

## 有限适配方案

1. 新增项目自己的仿真光照控制器，管理左右两盏固定位置的宽角白色 Spot；灯的方位、高度、范围和锥角在场景/配置中明确。使用原生 uGUI 控件接入现有仿真 UI，提供单侧、双侧、低/中/高和阴影开关。单侧可固定一个已命名侧，若另加左右选择则同步记录。
2. 双侧对称等强作为基准。可把当前档的 `totalIntensity` 当作合成强度预算：单侧用 T，双侧各 T/2；避免开第二盏灯就把总预算翻倍。但相加的 Unity intensity 不等于真实流明、实际落在板上的能量或相同像素亮度，仍要用灰色底板采样检查与调节。三档采用明确可修改数值，不称为实测 lux。
3. 默认使用固定弱环境光，固定材质/相机以隔离变量。只在该仿真上下文应用场景级设置，退出或禁用控制器时应考虑恢复原设置。原 Directional 可作为对照；进入 Spot 实验时明确禁用或一并记录其影响，不能留下未被说明的第三光源。
4. 阴影开关将受控灯设为 None 或 Soft，并设置/核对全局阴影质量、距离和质量档，检查积木/底板的投射与接收。采集记录实际生效值，不只保存 UI 上的“开启”。多盏实时灯可能增加渲染负担，应在现有渲染路径与材质上实际验证，不通过堆叠大量灯伪装连续面光源。

## 光照快照与独立 ID

沿用已有采集配对链路，在 JSON 新增独立 `lighting` 配置（具体字段名由实现统一），不把光照哈希混入已有 `cameraConfigurationId`：

- 配置 schema、预设标识、单/双侧、亮度档、合成总强度预算、阴影请求；这些说明实验意图。
- 每盏实际灯的稳定角色 ID（例如 left/right/baseline）、active/enabled、类型、世界姿态、颜色、强度、范围、Spot 角、cullingMask、renderMode、shadow 类型/强度/分辨率/bias/normalBias/nearPlane；按稳定顺序序列化。运行时额外灯若参与成像，应加入实际快照或使受控实验失败，避免默默漏记。
- RenderSettings 环境模式与颜色/强度、反射强度；QualitySettings 阴影质量/分辨率/距离/pixelLightCount；实际 activeColorSpace。若实验允许 cookie、动态反射或后处理变化，也应明确记录范围，不能把本次小快照称为完整渲染状态。
- `lightingConfigurationId`：与相机配置类似，对固定 DTO 的紧凑 JsonUtility JSON 取 UTF-8 SHA-256，带独立 schema/命名空间。排除 frameId、采集时间、布局版本和输出路径，确保再拍一帧不会改变灯光身份。非有限值直接拒绝。

以上字段与哈希是项目设计，不声称候选提供了同一稳定 ID 标准。配置 ID 代表已记录配置内容，不是实测照度认证；相机 ID 不变、灯光 ID 改变，是光照对照的预期行为。

在 Render 前固定 GT、相机与光照快照，渲染后重新取同范围配置比较；若事件或回调修改灯光、环境或阴影设置，拒绝保存并沿用现有配对写入的清理行为。借鉴 Perception 的“图像与配置属于同一次采集”机制，不引入其异步框架。

## 验证方案与完成边界

- 自动检查固定预设的总强度分配、单侧禁用另一侧、开关往返、三档单调且有限，以及独立 ID 在相同配置稳定、灯/环境/阴影变化后不同、帧/布局变化后保持一致。检查字段顺序固定与区域性不会改变 ID。
- Unity 6.3 实际运行相同布局，至少覆盖 2×3×2 组合；核对 UI、有效灯状态、PNG/JSON 配对。阴影开启/关闭用同亮度同布局对照图检查，确认有可见投影和未发生整板黑暗/过曝。俯视 RGB 是主要证据，不仅看 Scene 视图灯光图标。
- 使用中心、边缘、角落的固定灰色区域比较亮度分布，记录过曝/暗区；检查孤立与相邻积木底板附近的阴影。该检查证明合成控制按预期变化，不替代真机白平衡、镜头和投影干扰测试。
- 只调用既有 Unity API、自写控制器与数据 DTO，未新增第三方包或复制源码，因此本次不新增第三方版权代码声明。若未来复制实现或改变依赖，需另更新授权记录与 `THIRD_PARTY_NOTICES.md`。

本文件完成编码前的最小候选比较与适配方案。截至写入时，本任务没有修改代码/场景，没有宣称上述实现或检查已经通过；实施与实测结果由后续工作补充。

## 实施补充（2026-10-19）

已按上述原生 API 方案完成两盏 Spot、左/右/双侧、三档亮度、阴影开关与方向光对照；通过运行时 uGUI 弹出面板操作。采集顶层升级至 schema 3，光照 DTO 与独立 SHA-256 ID 使用 schema 1；记录所有场景灯和实际环境/阴影条件，并在渲染后拒绝混合参数快照。退出或禁用时恢复已接管的质量、环境和灯状态。未安装 Perception 或复制候选源码。

最终隔离 Unity Play 检查 200 项通过、21 对 PNG/JSON，原相机回归 684 项通过；主工程亦保存了六块积木的实际俯视样本。默认双侧标准无三通道白色饱和，高亮档确有局部饱和，作为压力条件保留。具体参数、各文件/方法职责、测试路径及近似边界见 [task-a-lighting-guide.md](../task-a-lighting-guide.md)。
