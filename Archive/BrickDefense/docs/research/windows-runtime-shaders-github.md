# Windows 独立程序的运行时着色器依赖

检索日期：2026-10-12。范围：构建当前版本并修复独立程序中积木/敌人材质初始化失败；不增加渲染插件。

## 验收与发现

第一次 Windows x64 构建成功，但实际运行日志报告 `LevelView.Awake` 和 `CombatView.Initialize` 在创建材质时 `ArgumentNullException: shader`。两处均动态查找 `Standard`。原场景没有序列化材质依赖，构建日志没有编译 Standard；编辑器内检查无法覆盖这一裁剪行为。

检索词：`Unity Shader.Find Standard Resources Material`、`Unity Standard.mat fileID 46`、`Unity AlwaysIncludedShaders`，限定 GitHub。

## 候选比较

| 项目 | 实际阅读及授权 | 匹配与差异 | 采用判断 |
| --- | --- | --- | --- |
| [Scrawk/Marching-Cubes-On-The-GPU](https://github.com/Scrawk/Marching-Cubes-On-The-GPU) | `README.md`、`Assets/MarchingCubesGPU/Scripts/MarchingCubesGPU.cs`、`LICENSE`（MIT） | `MakeGameObject` 对动态网格创建 Standard 材质，展示同类运行时需求；主要功能是 GPU 等值面，与本项目玩法无关 | 仅比较依赖方式；不复制源码或依赖 |
| [nickmeinhold/GhostHustlers](https://github.com/nickmeinhold/GhostHustlers) | README 的 Platform Notes 与项目结构；尝试获取 `ProjectConfigurator.cs`、`ProtonBeam.cs`、`LICENSE` 未成功，授权未确认 | Unity 6 动态材质，README 指出没有材质显式引用时透明变体会被裁剪；使用 URP/AR，不能直接接入 Built-in 项目 | 仅作问题机制参考；因源码与授权未取得，不采用代码 |
| [zoucdr/unity3d-mcp](https://github.com/zoucdr/unity3d-mcp) | `README.zh-CN.md` 内实际材质创建代码示例（创建 Standard 材质后调用 `AssetDatabase.CreateAsset`）；LICENSE 抓取失败 | 用持久材质保留资源引用的机制可参考；完整 MCP/编辑器工具不属于本项目需求 | 仅采用“保存材质资源”的机制，不复制代码或安装工具，授权未确认 |

## 整合方案

以 [Unity Shader.Find 官方说明](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Shader.Find.html) 作为行为依据：字符串查找不保留构建依赖；Resources 中引用着色器的材质可确保其被包含。

添加本项目的 `Assets/Resources/BrickSurface.mat`，显式引用内置 Standard，并保留当前使用的无贴图、无金属度、不透明材质变体。该资源由本项目定义，不来自第三方。`Sprites/Default` 已在原 GraphicsSettings 的 Always Included 列表中；不增加全量 Standard 变体或外部渲染系统。

验收使用真实非开发 Windows Player：确认积木与中文 UI 绘制，扫描/校对/发布可操作，敌人和射击正常，日志无 shader 空引用或其他运行时异常。仅 Editor 通过不能作为这项修复的验收。
