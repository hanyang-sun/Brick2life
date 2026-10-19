# 柔和策略界面：GitHub 检索与有限借鉴

日期：2026-10-11。用户已选择 D「柔和策略」方向。本文在运行时代码修改前完成。

## 范围与验收

依据 [首版需求](../requirements-v1.md) 的扫描预览、关卡预览和出生点选择要求，以及当前任务：调整现有 `DemoUI`、`PhotoScanPanel` 与 `LevelView` 的呈现。主页采用地图为主的小型浮动控制；照片页统一配色、层级和操作状态。地图使用暖色背景、低饱和积木、棋盘底座及柔和阴影。保持现有 10×10 实体格、20×20 导航格、逐块对象、示例切换、路线选择、照片导入、颜色标定、拒绝原因与确认流程。

验收：两种示例仍能切换且正确显示；出生点点击与显示位置一致；照片预览及样本颜色保真；校准、分析、确认的禁用条件有效；照片页完全遮盖三维地图；桌面宽屏与窄屏布局没有重叠、裁掉必要操作；运行 Unity 场景回归并检查实际 Game View 截图。本次仅改现有呈现，不加入战斗功能。Android 真机结果另行验证。

## 检索范围

实际进行限定 GitHub 的网络检索，关键词：

- `site:github.com Unity IMGUI rounded GUISkin runtime GUIStyle`
- `site:github.com UnityGUISkin light runtime UI`
- `site:github.com Unity-Technologies UIToolkitUnityRoyaleRuntimeDemo`
- `site:github.com "Unity" "GUISkin" "MIT" "rounded"`

搜索发现 Unity 原生 IMGUI 参考实现、官方运行时 UI 样例、uGUI 圆角组件。部分 GUISkin 搜索结果为编辑器皮肤或授权不清的代码，不作为采用依据。下表各候选均实际读取 README、相关实现及许可证；网页抓取可能有缓存，因此记录读取的分支/版本，不宣称主分支内容就是本机 Unity 版本或最新发布。

## 候选比较

| 候选与实际读取文件 | 授权及维护证据 | 匹配、兼容性与采用方式 |
| --- | --- | --- |
| [Unity-Technologies/UnityCsReference](https://github.com/Unity-Technologies/UnityCsReference)：`master` 的 [README](https://github.com/Unity-Technologies/UnityCsReference/blob/master/README.md)、[GUI.cs](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/IMGUI/GUI.cs)、[GUIStyle.cs](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/IMGUI/GUIStyle.cs)、[LICENSE](https://github.com/Unity-Technologies/UnityCsReference/blob/master/LICENSE.md) | [Reference-Only License](https://unity.com/legal/licenses/unity-reference-only-license)，仅检查机制，不能改编或分发源码。仓库随 Unity 版本更新；读到的 README 标注 7000.0.0a8，不能直接当成 6000.3.24 的源码。 | 与现有 Unity IMGUI 最接近。只理解公开 `GUI.DrawTexture`、按钮事件、`GUI.matrix`、`GUIStyle` 状态和滚动区域机制；在本机 6000.3.24 编译/运行验证。直接调用 Unity API，无新包、网络或渲染管线依赖；离线可用性是接口分析推断，Android 尚未实测。 |
| [Unity-Technologies/UIToolkitUnityRoyaleRuntimeDemo](https://github.com/Unity-Technologies/UIToolkitUnityRoyaleRuntimeDemo)：[README](https://github.com/Unity-Technologies/UIToolkitUnityRoyaleRuntimeDemo/blob/master/README.md)、[EndScreen.cs](https://github.com/Unity-Technologies/UIToolkitUnityRoyaleRuntimeDemo/blob/master/Assets/Scripts/UI/EndScreen.cs)、[HealthUI.uss](https://github.com/Unity-Technologies/UIToolkitUnityRoyaleRuntimeDemo/blob/master/Assets/UI/Uxml/HealthUI.uss)、[LICENSE.md](https://github.com/Unity-Technologies/UIToolkitUnityRoyaleRuntimeDemo/blob/master/LICENSE.md) | MIT，Copyright 2020 Unity Technologies。[提交历史](https://github.com/Unity-Technologies/UIToolkitUnityRoyaleRuntimeDemo/commits/master/) 中可见 2025-10-23 的 `9b5006d` 是移除 EmojiOne 资源，之前界面包更新主要在 2021 年；README 测试版本为 Unity 2020.1.0f1。 | 游戏界面的主操作、标题层级及缩放机制有参考价值。实际是 UI Toolkit/UXML/USS，并带 URP、Addressables 等完整游戏依赖；不是当前 IMGUI 的直接组件。只参考内容分层和视口缩放思路，保留现有 C# 入口。整套迁移成本高，且须构建 Addressables；没有 Android 离线验证证据。 |
| [kirevdokimov/Unity-UI-Rounded-Corners](https://github.com/kirevdokimov/Unity-UI-Rounded-Corners)：[README](https://github.com/kirevdokimov/Unity-UI-Rounded-Corners/blob/master/README.md)、[ImageWithRoundedCorners.cs](https://github.com/kirevdokimov/Unity-UI-Rounded-Corners/blob/master/UiRoundedCorners/ImageWithRoundedCorners.cs)、[RoundedCorners.shader](https://github.com/kirevdokimov/Unity-UI-Rounded-Corners/blob/master/UiRoundedCorners/RoundedCorners.shader)、[package.json](https://github.com/kirevdokimov/Unity-UI-Rounded-Corners/blob/master/package.json)、[LICENSE](https://github.com/kirevdokimov/Unity-UI-Rounded-Corners/blob/master/LICENSE) | MIT，Copyright 2019 Kirill Evdokimov。读取的包声明版本 3.5.0；README 提供 Unity 2019.3+ 包安装方式。这些信息不等于已验证 Unity 6 或 Android。 | 圆角在调整尺寸后保持比例、销毁创建的材质等机制可参考。实际依赖 `UnityEngine.UI` 的 `MaskableGraphic`/`RectTransform` 和自定义 Shader，直接引入会扩展现有 IMGUI 架构。只借鉴尺寸及资源生命周期原则；通过 Unity 自带圆角 API 绘制，不引入包或 shader。该源码没有必要的网络调用，但移动图形兼容性仍须真机验收。 |

## 已阅读的实现与公开 API 依据

1. 原生 `GUI.cs`：公开 `GUI.DrawTexture` 重载含 `color`、`borderWidth`、`borderRadius`；实现根据边框/圆角选择绘制路径。`Button` 委托到标准控件事件处理；`BeginScrollView` 使用独立内容区域和裁剪。采用这些公开 API，保持 Unity 的按钮点击及禁用行为。由于参考源码版本不同，另核对 [Unity 6 的 DrawTexture API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/GUI.DrawTexture.html)：文档列出相同圆角参数，并说明此绘制需要 Shader Model 2.5 或以上。
2. `GUIStyle.cs`：普通、悬停、按下、聚焦及选中状态分别设置；`border` 影响背景绘制，`padding` 与布局另处理。由此把主题样式集中缓存并清晰设置状态，避免全局 GUI 颜色污染照片。九宫格背景可作为未来需要材质皮肤时的方案；本次圆角优先使用原生绘制 API，不必生成九宫格资产。公开 [GUIStyle.border 文档](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/GUIStyle-border.html) 也确认背景边界与位置分离。
3. `EndScreen.cs`：通过 `UIDocument` 查询标签和按钮，设置内容，再绑定事件；可参考主操作与文字层级。其动画依赖当时实验性 UIElements API，本次不采用。`HealthUI.uss` 中背景、内容和框架分层，只用于理解图层职责。
4. 圆角组件：尺寸变化时更新圆角参数；`OnDestroy` 清理运行时材质。Shader 处理遮罩、透明混合和圆角透明度。当前 IMGUI 可以用原生绘制解决圆角，因此不复制其 C#、SDF 或 Shader。

## 风格参照及具体整合方案

[Dorfromantik 官方 Presskit](https://www.toukana.com/dorfromantik/presskit) 描述手绘桌游观感，是用户选定的视觉方向依据。采用暖白、鼠尾草绿、柔和蓝绿与赭色的组合，让棋盘及积木成为视觉中心；具体配色、排版和立方体材质由当前项目实现。官方截图仅用于风格观察，不下载进项目、不复制图标、模型、品牌或美术资源。

- **共享主题：**新增仅属于 Presentation 的 `SoftStrategyTheme`，封装颜色、文字层级、圆角卡片和按钮绘制。使用原生 `GUI.DrawTexture` 及 `Texture2D.whiteTexture`；需要的样式或小背景纹理缓存，退出/销毁时清理。保留标准 `GUI.Button` 事件，不重写点击系统。
- **主页：**顶部小标题与状态，底部两个示例入口及扫描入口，侧边简明角色图例。按钮使用明确的选中、悬停和禁用反馈。用现有 `LevelSession` 调用切换布局及选择出生点；阻止 UI 区域的点击落到棋盘，屏幕与 GUI 坐标转换经过验证。
- **棋盘：**保留导航格和逐块 Cube 的数量与定位，调整现有材质及轻微间隙，独立底座不混入导航格根节点，灯光和背景形成柔和桌面观感。核心/墙/塔及有效/无效路线仍有可区分颜色。
- **照片页：**全屏不透明暖色背景；宽屏照片与设置双列，窄屏单列滚动。保留输入模式、导入/相机、四个取样项、保存/重置、分析、问题列表、数字地图和确认。原始照片、采样 RGB、识别证据颜色与数字地图实体颜色保持原值，不套主题滤镜。异常诊断代码可继续供调试和报告使用。
- **边界：**Contracts、Domain、Vision、Application 的识别、状态、坐标规则不因主题改变；不新增依赖，不把 UI Toolkit/Canvas 或第三方 shader 塞入现有 IMGUI 流程。

采用性质为**机制参考与公开 API 整合**。本次没有外部源码改编、直接依赖或第三方美术资产；现有第三方通知保持原记录。若后续实际复制任何候选代码/资源，须重新确认授权并追加完整许可证与采用范围。

## 验证计划与限制

运行现有纯 C# 检查及隔离 Unity 场景检查，验证示例对象/路线、照片导入分析确认和失败保留旧地图。实际 Game View 分别检查主页、扫描空态、校准态、成功/拒绝态及窄屏页面，验证遮盖、坐标点击和可滚动操作。截图检查包含 IMGUI，不能只用 Camera.Render 代替。界面变更不会构成识别准确率或 Android 真机性能的验证结果。

## 实施结果（2026-10-11）

实际使用原生圆角绘制、缓存文字样式与仅供照片页使用的 GUISkin 副本；两张 2×2 纯色纹理提供滚动条配色，销毁面板时清理主题自有 Skin/Texture。控件仍由 GUI.Button 处理输入，GUI 状态在 finally 中恢复。未复制参考项目源码，也未引入第三方包或图像。

逻辑、Unity 场景及真实 Game 视图交互/截图检查通过。1280×801、862×679、540×801 窗口下完成例子切换、棋盘选点、原 JPG 核心取色、保存、分析、确认、裁切拒绝及窄屏滚动。原图 RGB 和不透明页面通过像素核对。当时的截图与操作结果见 [验证目录](../../.scratch/soft-strategy-ui/verification/README.md)。
