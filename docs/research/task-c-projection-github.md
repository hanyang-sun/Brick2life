# 任务 C 虚拟投影与游戏反馈：GitHub 检索及有限借鉴

日期：2026-10-25。目标环境：Unity 6000.3.24f1（工程 `ProjectSettings/ProjectVersion.txt`）、Built-in 渲染管线、10×10 单层积木、顶置实验相机（`TopCamera`，`m_TargetTexture` 512×512 RenderTexture）。

## 本次范围与验收依据

用户 2026-10-25 指定完成分工任务 C（`fengong.docx` 第三节、`Brick2Life_Research_Guide.docx` 11.1）：

- C 的主文件范围：`SimulationLevelAdapter`、`ProjectionSimulator`；只消费 B 的预测事件；负责反馈映射与场景分层。
- 任务书要求：接收 B 输出的积木状态与变化事件 → 语义映射（例如红色代表防御塔、灰色代表城墙）→ 更新 Unity 游戏世界 → 模拟顶置投影仪在积木或桌面显示颜色、格点或建造提示 → 反馈位置与格位一致。
- 任务书明确的边界：初期可用简单颜色、箭头或高亮；但投影必须影响虚拟物体的可见表面，不能只是叠在游戏界面上的 UI 图片。

项目内依据：`.scratch/task-a-simulation/spec.md` 的分阶段交付第 2 项写明“独立投影仿真：任务 C 用明确标注的人工目标或测试数据驱动投影，验证积木/桌面表面显示及位置对齐；这项测试不作为视觉识别已接通的证据”，第 3 项才是“B 的预测状态驱动游戏与投影更新”。`docs/architecture.md` 规定 Vision/Projection 随实现落地，Core/Application 不得引用 Unity。`docs/implemented-features-2026-10-18.md` 第 5 节把“视觉预测状态更新游戏内容”“虚拟投影作用于桌面和积木表面、对齐与遮挡验证”列为未实现项。

因此本轮范围是：新增 Unity-free 的语义映射与投影计划逻辑、两个 Unity 消费组件（关卡视图与投影）、专用投影层与顶置相机分层、独立投影仿真演示入口（人工标注目标），并如实区分“已验证”和“未在 Unity 中验证”。

## 检索记录

检索日期：2026-10-25。关键词：`Unity Projector component Built-In Render Pipeline`、`unity texture projector shader unity_Projector tex2Dproj`、`unity dynamic decals built-in render pipeline`、`unity grid cell highlight overlay`、`Unity Projector orthographic ignore layers`。

候选充足性说明：本轮需要的是“在 Built-in 管线下把带语义颜色的内容贴到指定格位表面”的能力。社区实现主要有两类：官方 `Projector` 组件配合投影 shader，以及运行时贴花/投影插件。两类都能找到可读的公开实现，因此按 AGENTS.md 要求比较两个以上候选。

| 候选 | 实际阅读内容 | 兼容、依赖、授权 | 匹配点与差异 | 结论 |
| --- | --- | --- | --- | --- |
| [Unity 6.3 Manual: Projector component（Built-In Render Pipeline）](https://docs.unity3d.com/6000.3/Documentation/Manual/class-Projector.html) | 组件属性表：Near/Far Clip、Field Of View、Aspect、**Orthographic**、**Orthographic Size**、Material、**Ignore Layers**；并核对其父页 [Decals in the Built-In Render Pipeline](https://docs.unity3d.com/6000.3/Documentation/Manual/decals-birp.html) 与 [Prepare materials for projection](https://docs.unity3d.com/6000.3/Documentation/Manual/prepare-materials-projection.html) | Unity 第一方组件，Built-in 管线原生，无额外包；官方文档 | 匹配：顶置正交投影、按层忽略、材质驱动，最接近真实投影仪语义。差异：需要自备投影 shader 与投影贴图，逐格多色要动态纹理；本机无 Unity 6.3，无法实测投影矩阵与贴图更新 | 记录为**升级路径**，本轮不依赖组件实现，避免提交未经验证的渲染配置 |
| [XJINE/Unity_TextureProjector](https://github.com/XJINE/Unity_TextureProjector) | [README](https://github.com/XJINE/Unity_TextureProjector/blob/main/README.md)、[TextureProjector.shader](https://github.com/XJINE/Unity_TextureProjector/blob/main/Assets/TextureProjector/TextureProjector.shader)（完整 `Shader "Projector/TextureProjector"`，使用 `unity_Projector` 矩阵与 `tex2Dproj`）、[LICENSE](https://github.com/XJINE/Unity_TextureProjector/blob/main/LICENSE) | BSD-3-Clause (c) 2017 XJINE；单 shader + 材质 + 预置体，无外部依赖；改用其 shader 需保留版权声明 | 匹配：一行 shader 即可把纹理投到表面，正是 Projector 组件所需的材质。差异：只有一张 `_MainTex`，没有逐格语义、没有对齐信息，也无 Unity 6 验证 | **只借鉴机制**（`unity_Projector` + 投影贴图），不复制 shader 代码；如后续切换到 Projector 方案再评估引入并登记授权 |
| [EricFreeman/DynamicDecals](https://github.com/EricFreeman/DynamicDecals) | [README](https://github.com/EricFreeman/DynamicDecals/blob/master/README.md)（原 Llockham-Industries 商店插件开源版；运行时贴花、layer 掩码、additive/multiplicative 投影、支持 Built-in 前向/延迟） | 仓库根目录没有 LICENSE 文件；README 只说明原作者同意开源，**未给出可核对的授权条款** | 匹配：贴花/投影的工程组织（运行时生成、按层遮罩、叠加模式）与“投影必须进入相机图像”的思路一致。差异：插件体量与编辑器工具远大于本项目需要；授权不明确；无 Unity 6 兼容声明 | **只作思路参考**，不引入代码或资源 |
| [Unity-Technologies/UnityCsReference](https://github.com/Unity-Technologies/UnityCsReference) | 本仓库既有研究已读其 `LICENSE.md` 与 Reference-Only 许可说明；本轮只用于核对公开 API 名称（`Projector`、`Mesh`/`Quad`、`MaterialPropertyBlock`、`Camera.cullingMask`、`Renderer.shadowCastingMode`） | Reference-Only License：不得改写后分发参考源码；只调用公开 API 时不复制实现 | 匹配：确认本项目使用的都是公开 API。差异：不是可直接复用的功能实现 | **API 参考**，不复制源码 |
| 本项目归档 `Archive/BrickDefense`（旧 `LevelPipeline`/`LevelView`） | 旧关卡语义只有 core/wall/tower 三类角色与 10×10→20×20 的占用映射；相关内容已在 `docs/implemented-features-2026-10-18.md` 与本仓库 `THIRD_PARTY_NOTICES.md` 记录 | 自有代码；当年的战斗逻辑部分改编自 Brackeys 教程（Unlicense） | 匹配：角色集合可直接沿用，避免重新发明语义。差异：旧模块依赖整套塔防运行时，已被 2026-10-20 的退役决定移出编译范围 | **只沿用角色词汇**，不迁入旧代码 |

## 本轮采用方案

1. **语义与投影计划放在 Unity-free 的 Application 层。** 新增 `LevelSemantics`（颜色→角色映射、关卡状态与合法性）、`ProjectionPlan`（逐格投影标记）、`LevelFeedbackSession`（消费 `BoardUpdate`，产出关卡状态与投影计划）。这样 C 的规则可以被纯 C# 检查覆盖，Unity 侧只负责把计划画出来；既符合 `docs/architecture.md` 的依赖约束，也避免“看起来验证过”的渲染代码。
2. **只消费预测事件。** 输入固定为 `Brick2Life.Application.BoardUpdate`（由 `BoardSyncSession` 在确认视觉观测后产出）。C 不读 `SimulationBoardSession.Snapshot()`、不读关卡真值，也不创建观测。`Rejected`/`Stale` 只保留上一次已确认状态并回报原因。
3. **投影用“逐格表面贴合面片”实现，而不是 UI 覆盖。** 每个投影标记是一个 3D 四边形，贴在对应格位的可见表面（有积木取积木顶面高度，空格取桌面高度）之上，带 alpha 混合，进入相机渲染；因此顶置相机采集到的 RGB 会因为投影而改变，这正是任务书要求的“影响可见表面”。这不是光学投影仿真：没有镜头畸变、亮度衰减、梯形校正；这些限制写入手册，后续可用官方 `Projector` 组件 + 投影贴图替换渲染层，而不改变 Application 层接口。
4. **场景分层。** 新增两个用户层：`Brick2LifeGame`（数字游戏对象）与 `Brick2LifeProjection`（投影面片）。顶置相机必须看不到游戏对象（否则虚拟塔会被识别成积木），但必须看到投影（投影是真实光）；操作相机两者都要看到；UI 层继续被操作相机排除。分层同时给“投影开/关对照”提供开关：关闭投影层对象即可得到同布局的对照图像。
5. **独立投影仿真入口。** 依据 `.scratch/task-a-simulation/spec.md` 第 2 阶段，提供一个明确标注为人工目标（非识别结果）的演示驱动组件，按固定样例布局向 `BoardSyncSession` 提交 `FrameObservation`，从而走完整的 `BoardUpdate` → C 消费链路。接入 B 后禁用该组件即可。

## 授权结论

本轮不复制、不依赖任何第三方代码：Projector 组件为 Unity 内置，XJINE 的 shader 只作为机制参考，DynamicDecals 只作思路参考，UnityCsReference 只用于 API 核对。因此不修改 `THIRD_PARTY_NOTICES.md`。若后续改用 XJINE 的 shader 或引入 Projector 材质资产，需要在该文件登记 BSD-3-Clause 版权声明。

## 验证方案与边界

- 纯 C#（可执行）：颜色→角色映射、单一核心与未映射颜色的判定、`ProjectionPlan` 的标记种类/数量/表面选择/变更高亮、`LevelFeedbackSession` 对 Baseline/Changed/Unchanged/Rejected/Stale 的处理、版本与幂等。
- Unity 侧（本轮无法执行，本机只有 2022.3.62f3c1，工程要求 6000.3.24f1）：面片与格位对齐、投影进入顶置相机图像、投影开/关对照、分层掩码、`Projector` 升级路径。
- 编译级验证：用 Roslyn 编译 Application 检查项，用 Unity 2022.3 的托管程序集对 `Assets/Scripts` 与 `Assets/Editor` 做语法与 API 基线编译（不替代 Unity 6.3 实际编译）。

以上检索记录与适配方案在编码前完成；实施结果与未覆盖项见 `docs/task-c-feedback-guide.md` 与 `.scratch/task-c-feedback/verification/README.md`。
