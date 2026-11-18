# Screenberry 投影纹理接入

2026-11-09 在 GitHub 检索 `Unity Spout RenderTexture sender`、`KlakSpout Unity 6.3` 和 `Spout2 DirectX SendTexture`。本次接入用于同一台 Windows 电脑上的仿真联调。验收时，Screenberry 应能收到 Unity 的投影纹理，并在虚拟相机中显示投到棋盘上的画面。

| 候选 | 阅读内容与授权 | 适用性 |
| --- | --- | --- |
| [keijiro/KlakSpout](https://github.com/keijiro/KlakSpout) | README、2.0.6 发布说明、发布包中的 `SpoutSender.cs`、`SpoutSender_Properties.cs`、`SpoutSenderEditor.cs` 与许可证。C# 封装采用 Unlicense。 | 提供 Unity 6.3 和 Direct3D 11/12 下的纹理发送，直接使用 UPM 发布包 2.0.6。Spout 联调入口仅在 Windows 编辑器启用。 |
| [leadedge/Spout2](https://github.com/leadedge/Spout2) | README、BSD-2-Clause 许可证、`SpoutDX.cpp` 的 `SendTexture` 和共享纹理逻辑。 | 原生 SDK 可实现同一进程间协议，直接调用需要维护 Unity 图形插件和生命周期。仅用作机制参考。 |

通过 KlakSpout 的 `Texture` 捕获模式发送 `SimulationProjectedLight.OutputTexture`，保留源纹理尺寸。投影关闭时发送黑帧，标定图启用时发送标定纹理。通过 [Screenberry Spout In](https://help.screenberry.com/en/node-reference/nodes) 接收后接入 Projection Mapper。Screenberry 的方形投影画幅、位置和视场角与 Unity 的默认虚拟投影参数配对，Android 离线运行路径沿用已有实现。

## 顶置相机预览

2026-11-09 补充检索 `Unity Spout camera Render Texture`。参阅 [Unity-OBS-AlphaWorkflowSample](https://github.com/keijiro/Unity-OBS-AlphaWorkflowSample) 的相机渲染目标与 Texture 捕获流程，并比对 KlakSpout 2.0.6 的 `SpoutSender.CaptureFrame` 和 Spout2 的 `SendTexture`。示例使用 URP 的透明合成，Brick2Life 使用 Built-in 的完整 RGB 画面。仅参考示例配置，继续使用现有 KlakSpout 依赖。

将 `SimulationCameraCapture` 的渲染目标作为 `Brick2Life Board RGB` 独立发布到 Screenberry Canvas。它保留 Unity 实际棋盘、积木以及表面投影。`Brick2Life Projector` 仍提供纯投影图像，用于独立的 Screenberry 虚拟投影路径。验收时在 Unity 增减积木并检查 Canvas 的画面跟随变化。
