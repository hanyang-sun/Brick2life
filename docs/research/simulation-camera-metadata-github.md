# 仿真采集相机配置记录：GitHub 检索与有限借鉴

检索日期：2026-10-19。目标环境：Unity 6000.3.24f1、Built-in Render Pipeline。

## 范围与验收条件

本轮用户授权在现有 PNG＋GT JSON 采集中追加相机配置记录。保持现有相机投影方式、场景、预览、棋盘状态和文件配对行为。当前任务 A 的仿真需求优先于 `docs/requirements-v1.md` 中历史实体照片流程；不借此实现透视切换、镜头畸变、曝光模拟或投影反馈。

验收目标：每个 JSON 能说明对应 PNG 的投影类型、视场/正交尺寸、世界姿态、有效相机矩阵和输出分辨率；相同配置具有稳定配置 ID，参数变化可区分；不得以 Unity 实例 ID 或每帧随机 ID 代替配置身份。已有 `frameId`、GT、四角标定字段保持可读，不把模拟参数称为真机实测标定。

已阅读项目 `AGENTS.md`、`docs/agents/github-reuse.md`、`docs/agents/domain.md`、历史需求及既有相机预览研究；本项目尚无根目录 `GLOSSARY.md` 和 `docs/adr/`。已读 `SimulationCameraCapture.cs` 的采集、四角像素约定和 JSON DTO，适配既有一次手动保存链路。

## 检索范围与实际阅读

关键词：`UnityCsReference Camera.bindings.cs JsonUtility`、`site:github.com/Unity-Technologies/com.unity.perception camera metadata projection`、`PerceptionCamera RegisterNewCapture`。通过 GitHub 仓库页面、raw 源文件及官方 Unity 6000.3 API 文档核实，不以搜索摘要代替阅读。

| 候选 | 实际读过的文件与机制 | 授权、兼容与成本 | 选择 |
| --- | --- | --- | --- |
| [Unity-Technologies/UnityCsReference](https://github.com/Unity-Technologies/UnityCsReference)，master | [README](https://github.com/Unity-Technologies/UnityCsReference/blob/master/README.md)、[Camera.bindings.cs](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Camera/Camera.bindings.cs)、[JsonUtility.bindings.cs](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/JSONSerialize/Public/JsonUtility.bindings.cs)、[LICENSE](https://github.com/Unity-Technologies/UnityCsReference/blob/master/LICENSE.md)。相机公开投影类型、FOV、正交尺寸、裁剪面、aspect、视图/投影矩阵；JsonUtility 面向满足 Unity 序列化规则的字段容器。 | README 当前标注 Unity 7000.0.0a10，因此仅作 API 机制参考，不能将 master 当作 6000.3.24f1 源码。Reference-Only License 仅允许参考，不允许复制改写后分发。调用当前编辑器公开 API，无包依赖、离线运行可用；Android 实机仍需验证。 | 采用公开 Camera API 取值与自有可序列化 DTO；不复制仓库源码。 |
| [Unity-Technologies/com.unity.perception](https://github.com/Unity-Technologies/com.unity.perception)，main | [README](https://github.com/Unity-Technologies/com.unity.perception/blob/main/README.md)、[LICENSE](https://github.com/Unity-Technologies/com.unity.perception/blob/main/LICENSE.md)、[PerceptionCamera.cs](https://github.com/Unity-Technologies/com.unity.perception/blob/main/com.unity.perception/Runtime/GroundTruth/PerceptionCamera.cs)、[package.json](https://github.com/Unity-Technologies/com.unity.perception/blob/main/com.unity.perception/package.json)、[PerceptionCamera 文档](https://github.com/Unity-Technologies/com.unity.perception/blob/main/com.unity.perception/Documentation~/PerceptionCamera.md)。重点阅读 `RegisterNewCapture` 和 `OnRGBTextureOutputTextureReadback`：渲染帧内记录姿态、projection、dimension、matrix，并按帧号关联随后完成的图像编码。 | Apache-2.0。README 已声明停止官方维护，标注版本为 2021.3/2022.1；文档相机面向 HDRP。包依赖 Burst、Collections、Newtonsoft JSON、render-pipelines.core 12.1.7。不能宣称已支持当前 Unity 6 Built-in/Android；整包接入和迁移成本超出本次字段扩展。 | 仅参考“图像与元数据共享一次采集快照”的机制。不安装包、不迁移渲染管线、不复制代码。 |

许可证已实际阅读：[Unity Reference-Only License v1.1](https://unity.com/legal/licenses/unity-reference-only-license) 限制为参考使用；Perception 的 Apache-2.0 允许依条款复用，但本次没有代码改编或直接依赖。当前方案不新增第三方版权代码，也无需追加第三方依赖声明；若实施范围改变，应重新核对并更新 `THIRD_PARTY_NOTICES.md`。

## 6000.3 API 核实与适配方案

官方 [Camera.projectionMatrix](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Camera-projectionMatrix.html) 与 [Camera.worldToCameraMatrix](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Camera-worldToCameraMatrix.html) 确认可取得实际视图/投影矩阵。记录矩阵是为了识别自定义矩阵等单靠姿态/FOV无法完整说明的情况。视图矩阵相机空间前方为负 Z，不能直接把它称为 OpenCV 的外参约定；Unity 4×4 投影矩阵也不等于以像素为单位的 OpenCV 3×3 内参 K。

使用现有 JSON 中追加的 `cameraConfiguration` 数据容器。建议字段：

- 配置 schema 版本、`projectionType`（`orthographic` / `perspective`）、`fieldOfViewDegrees`（垂直角度）、`orthographicSize`（半高度）、`aspect`、near/far、归一化 viewport `rect`；保留两种投影的配置值，并由类型说明实际生效方式。
- 世界位置与四元数旋转，明确分量为 x/y/z/w；不依赖名称或 `GetInstanceID()` 重建配置。
- `worldToCameraMatrix`、`projectionMatrix` 展平为各 16 个有限数，明确 row-major、顺序 m00…m03、m10…m13、m20…m23、m30…m33，列向量计算顺序为 projection × view × worldPoint。不要将平台相关的 GPU 转换矩阵混入同一字段。
- PNG 实际 `imageWidth/imageHeight`，取绑定 RenderTexture 的宽高而非 Game 视图或桌面屏幕；保留已有左上原点、整数像素中心约定。
- 可按实际相机能力记录 physical camera 开关、focalLength、sensorSize、lensShift、gateFit；它们是配置快照，不代表已经模拟真实畸变/曝光。本次最小实现可依有效矩阵完整记录投影几何，而不扩展成摄影传感器模型。

官方 [JsonUtility.ToJson](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/JsonUtility.ToJson.html) 说明使用 Unity 序列化规则。沿用自有 `[Serializable]` 类和公开字段，将数组包在 DTO 中，不直接序列化 Camera 组件或顶层数组。无需增加 Newtonsoft/OpenCV/Perception 运行库。

## 稳定配置 ID：本项目设计

两个候选提供字段与采集关联机制，并未提供可直接照搬的跨运行配置哈希标准。以下属于本项目设计，不声称源自候选实现：

1. 把配置版本、字段固定顺序、投影类型、输出宽高、姿态和矩阵等所记录的配置值组成确定性输入。最小方案可对固定字段 DTO 的紧凑 `JsonUtility.ToJson` 结果取 UTF-8 哈希，将此序列化规则连同 schema 版本固定下来，并实际验证跨实例和区域性稳定；不能对任意字典或随意改变序列化器后的 JSON 承诺跨实现一致。若以后需要语言无关的规范哈希，再显式规定字段顺序和 invariant culture、可往返精度数值格式，或固定字节序二进制编码。拒绝 NaN/Infinity，明确负零是否规范化。
2. 用 SHA-256 生成带版本前缀的 `cameraConfigurationId`。不要用 .NET `GetHashCode()`、Unity InstanceID、GUID 或对象 JSON 字段顺序的偶然行为代替稳定编码。无需把第三方源码引入项目。
3. 不纳入 `frameId`、采集时间、Unity 帧号、布局版本或文件路径：换摆法或再拍一张不会改变相机配置 ID。配置 ID 表示所记录配置的内容身份，不表示图像内容相同，也不能覆盖未记录的光照、材质或后处理变化。
4. 应保存所哈希的完整配置字段，而非只有 ID，方便复核和未来版本迁移。独立 schema 版本避免未来增加字段时出现无解释的 ID 变化。

## 采集一致性与验证计划

- 在目标 RenderTexture 生效的同一采集上下文内取得配置快照、四角和 GT，再执行既有 Render。渲染前后比较有效配置；若回调改变了矩阵/姿态/尺寸，拒绝发布混合来源的 PNG＋JSON。避免图像完成后才随意读取已变化的相机。
- 保留当前配对文件写入与最后发布 JSON 的行为。配置记录成功前不宣称本次图像可用于标定。
- 有意义的自动验证：相同输入跨实例得到相同配置 ID；帧号/布局/时间变化不影响 ID；FOV、正交尺寸、姿态、矩阵、尺寸分别变化时可区分；矩阵展平顺序可独立还原；不同区域性设置编码一致；非法数值被拒绝。
- 使用 Unity 实际采集至少一个正交与一个透视样本，核对 PNG 解码尺寸、JSON 类型/参数和 16 元矩阵。测试可在隔离工程进行，不要求更改当前正式场景。以导出矩阵投影已知点与现有四角导出作一致性检查，但该检查只证明元数据配对，不能代替独立图像测试点误差评测或真机标定。

本记录完成编码前最小候选比较，实施和验证结果应由实现者另行填写；截至本记录写入时，本任务未修改代码或场景，未声称测试已通过。

## 实施结果（2026-10-19）

已在现有 `SimulationCameraCapture.cs` 中实现配置快照和 `cameraConfigurationId`，顶层导出 schema 升为 2，相机配置 schema 为 1。固定 DTO 由 JsonUtility 紧凑序列化后计算 UTF-8 SHA-256，带 `camera-v1-` 前缀；纳入 Unity 版本，拒绝非有限数并规范化负零。未新增运行库依赖或复制第三方代码。

独立 Unity 6000.3.24f1 工程实际渲染验证通过 684 项断言，生成 15 对 PNG/JSON，覆盖正交、透视、Physical Camera、尺寸/矩阵/姿态变更、跨实例与区域性稳定及渲染期间变更拒绝。运行方式和本地结果链接见 [相机配置说明](../task-a-camera-metadata-guide.md)。这些检查验证采集元数据配对，不代表标定精度或真机可靠性已经验收。
