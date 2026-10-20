# 左右光强比例与环境光倍数：编码前检索和适配

检索日期：2026-10-19。目标：Unity 6000.3.24f1、Built-in、现有任务 A 单层仿真。

## 范围与验收

用户授权在现有单侧/双侧、三档亮度、阴影开关基础上新增左右光强比例和环境光强度控制。本轮保留相机、镜头、曝光与畸变范围；使用已安装的原生 uGUI Slider，不增加包。验收不仅核对数值，还要在固定布局下观察 RGB 的照明变化，并在窄窗口保证控件可达。

依据已读 `AGENTS.md`、`docs/agents/github-reuse.md` 与 `docs/agents/domain.md`，以及前序 [光照控制研究](simulation-lighting-controls-github.md)。本轮复核第一方资料与当前 `SimulationLightingSettings`、`SimulationLightingControls`、`SimulationLightingConfiguration`、`SimulationCameraCapture` 的有关字段。当前光照配置 schema=1、哈希前缀 lighting-v1，外层 GT schema=3。

## 本轮检索与两个候选的实际阅读

关键词/目标：`Unity 6000.3 ambientIntensity Flat ambientLight`、`UnityCsReference GraphicsManagers RenderSettings`、`Unity Perception Phase2 light intensity scaling`、`uGUI 2.0 Slider ScrollRect`。通过 GitHub 原文件与官方 API 文档复核，不将搜索摘要当作实现依据。

| 候选 | 实际读过的内容 | 授权、兼容、成本与采用方式 |
| --- | --- | --- |
| [Unity-Technologies/UnityCsReference](https://github.com/Unity-Technologies/UnityCsReference) | 本轮重读 [README](https://github.com/Unity-Technologies/UnityCsReference/blob/master/README.md)、[GraphicsManagers.bindings.cs](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Graphics/GraphicsManagers.bindings.cs) 的 RenderSettings 字段与 ambientLight→AmbientSkyColor 绑定、[LICENSE](https://github.com/Unity-Technologies/UnityCsReference/blob/master/LICENSE.md) 及链接的 [Reference-Only License](https://unity.com/legal/licenses/unity-reference-only-license)。 | README 为 Unity 7000 参考源码，不能当作当前 6000.3 安装版本。许可仅供参考，不复制或改写分发源码。用现有公开 API 和自有控制器完成，无新依赖，离线运行；具体效果在 Unity 6.3 中核验，Android 实机未验证。 |
| [Unity-Technologies/com.unity.perception](https://github.com/Unity-Technologies/com.unity.perception) | 本轮重读 [README](https://github.com/Unity-Technologies/com.unity.perception/blob/main/README.md)、[LICENSE Apache-2.0](https://github.com/Unity-Technologies/com.unity.perception/blob/main/LICENSE.md)、[Phase2 完整教程实现](https://github.com/Unity-Technologies/com.unity.perception/blob/main/com.unity.perception/Documentation~/Tutorial/Phase2.md) 的 MyLightRandomizer 与 MyLightRandomizerTag，重点是 normalized 输入映射到每盏灯自己的 min/max，并将计算值写入 Light.intensity；也复核其双 Directional 不同强度示例。 | 旧 HDRP 教程，README 已声明停止官方维护；本项目没有引入随机化框架的必要。不安装包，不复制其实现。仅借鉴“归一化控制值→明确的实际光强”机制，改为确定性左右分配和用户控制。 |

两候选都没有直接给出本项目“固定总预算左右分配＋Flat 环境光独立倍数”的现成组件；以下为基于公开 API 的项目适配设计。未新增第三方代码或依赖，不新增版权声明；若采用方式改变，应另核对并更新 THIRD_PARTY_NOTICES。

## Flat 环境光：API 的关键区别

[Unity 6.3 RenderSettings.ambientIntensity](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RenderSettings-ambientIntensity.html) 明确：它乘的是 Skybox 生成的环境光，只有 AmbientMode.Skybox 且有天空盒材质时生效；其他环境模式直接读取颜色，改变该值无效。因此不能把它作为当前 Flat 模式的亮度滑块，更不能称为曝光。

[Unity 6.3 RenderSettings.ambientLight](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RenderSettings-ambientLight.html) 明确它是 Flat 环境光颜色，并与 ambientSkyColor 同值。适配应继续使用 AmbientMode.Flat，保存独立的基础 ambientColor 与用户倍数 ambientMultiplier，然后把 RGB 各分量乘倍数后写入实际 ambientLight，alpha 保持 1。保留/记录实际 ambientIntensity 是为了回放和诊断，不让它承担 Flat 倍数功能。

建议倍数范围 0～3，默认 1，以保持此前默认效果；0 表示关闭这一项环境填充，不表示直射灯、反射或全部画面都必定变黑。该范围为合成实验设计，未标定为真实照度。改变倍数后不覆盖基础颜色，避免反复拖动累乘导致漂移；禁用组件仍沿用既有全局状态恢复行为。

## 左右比例与 UI 适配

以当前亮度档计算的强度预算 T 和 leftShare=s 为输入：双侧时 leftIntensity=T×s、rightIntensity=T×(1−s)。s 范围 0～1、默认 0.5；两个实际强度之和保持 T。这里不是 T×s÷(1−s)，端点不产生除零。Unity intensity 预算保持不变不保证板面像素亮度完全不变。

Left 模式为左 T、右禁用；Right 模式反之；DirectionalReference 沿用既有行为。上述单侧/参考模式不受 s 影响，但可保留 s 作为返回 Dual 时的请求值；相应比例滑块可禁用，清楚显示仅双侧生效。避免端点使用强度零与 Light.enabled 的组合产生含糊状态，实际记录两者。

原生 [uGUI Slider](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-Slider.html) 支持 min/max、连续值与变化事件；使用两个现有 UI 风格的滑块，比例 UI 可显示 0～100 再换算为 s，显示“左 x% / 右 y%”及“环境光 x.xx 倍”。提供一个恢复 50/50 和环境 1 倍的按钮。初始化、程序更新和模式切换需防止通知回路；控件读取当前设置而不是每次重建后擅自重置默认。

新增控件不能只在大窗口检查。弹窗可沿用现有纵向布局，在高度不足时使用已有 uGUI [ScrollRect](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-ScrollRect.html) 的 viewport/content 组织，固定返回/收起按钮；确保滑块手柄可拖动、各项控制可滚动到、数值标签不被裁切、采集入口可达，并验证滚轮不会误触棋盘编辑。以实际最小窗口截图证明控件可用，不硬编码只适合当前屏幕的固定位置。

## 元数据和配置身份迁移

光照快照同时记录控制意图和渲染实际值：预设请求新增 leftShare、ambientMultiplier、基础 ambientColor；保留布局、亮度档、总强度预算等。真实 lights[] 继续记录实际 intensity/enabled/active，真实环境值继续记录 ambientMode、ambientSkyColor/ambientLight 对应颜色以及 ambientIntensity。

将光照 DTO schema 由 1 升为 2、lightingConfigurationId 前缀由 lighting-v1- 升为 lighting-v2-；外层 GT 保持 3，由嵌套光照 schema 表达这次新增字段与哈希规则变化，保留原字段。相机配置及相机 ID 不因新增光照控制改变。既有外层 3/光照 1 数据保留，读取时按嵌套版本解释；缺失新请求字段不能无条件当作零，旧数据的默认比例应按旧双侧等强即 0.5 理解，环境倍数按旧默认 1 理解。

沿用固定 DTO 的紧凑 JSON→UTF-8→SHA-256，排除帧、时间和布局。ID 包含用户请求字段，因此在单侧模式调了保留比例时，即使 RGB/实际两灯未变，光照 ID 也可能变化；它是完整记录的身份，不是像素指纹。渲染前后快照比较继续覆盖新字段与真实环境值，拒绝记录与图像不一致的采集。

## 有效验证设计

1. 在 Unity 中逐项实际应用：Dual 的 s=0、0.25、0.5、0.75、1 保持左右强度总和 T，0.5 重现旧等强行为；同样拖动比例时 Left/Right 的实际单侧强度不变，切回 Dual 恢复请求比例。对非法数值做有限性检查，不让 NaN/Infinity 进入场景或哈希。
2. 在 Flat 下以固定直射灯与布局批量采集环境倍数 0、1、3 的 PNG＋JSON，确认实际 ambientLight 与基础 RGB 成比例，并在未过曝的阴影区域统计 RGB 亮度有变化。不要仅断言 setter 数字，也不要只改 ambientIntensity 后看标签。保存同条件截图与 ROI 统计，避免用整幅平均值掩盖局部饱和。
3. 固定环境与总预算，采集比例 0.25、0.5、0.75 的俯视 RGB，对照积木两侧底板阴影和亮度变化；同一布局/相机的 camera ID 应保持，lighting ID 可区分且重复采集稳定。比较与此前默认条件一致的样本，防止默认状态意外改变。
4. 实际点击/拖动 UI，并在常规窗口与窄/低窗口检查文字、可点击区域、滚动和保存按钮。窗口尺寸可包含当前常用分辨率以及 800×600、640×480 等压力尺寸，但通过门槛应依据真实 UI 支持范围，不声称未经截图/输入验证的适配已成功。
5. 保持采集期间变更检测；加入一次渲染回调改变比例/环境的情形，确认不会发布参数与图像错配的 JSON。检查旧版本数据仍可识别版本，记录新 ID 前缀与 schema。

截至本研究文件写入时，候选与 API 核实已完成；本任务只新增该文件，没有修改代码/场景，也没有声称上述验收已经通过。

## 实施补充

后续已按此方案完成现有控制器、uGUI 滑块/滚动面板和光照 DTO 扩展，无新包和外部代码复制。最终采用字段 `leftLightShare`、`ambientMultiplier`、`baseAmbientColor`；光照 schema 2 / `lighting-v2-`，外层 GT 3 与相机编号算法保持。

Unity 6000.3.24f1 隔离 Play 验证 **334 项断言通过、34 对 PNG/JSON**，相机回归 **684 项通过**。实际采样中，25:75 与 75:25 对称灰色区域从 134/183 翻转为 183/134；环境 0/1/3 倍对应同一区域约 135/159/206。比对实际灯强、真实 ambientLight、请求元数据和配置身份，并通过指针拖动及三种窗口尺寸的点击/滚动检查。主工程已保留当时运行中的 10 块积木并实际保存新 schema 样本。方法职责、结果路径与重现说明见 [光照指南](../task-a-lighting-guide.md)。
