# 展示积木塑料材质与独立预览光照

日期：2026-10-30。用户确认将展示窗口的纯色积木改成柔和受光的塑料材质，投影图案仍保持清晰明亮。范围仅限 Editor 的独立预览，不改变源调色板、RGB 相机或识别算法。

## 检索与比较

本轮在 GitHub 检索 `PreviewRenderUtility lights ambientColor` 和 `RuntimePreviewGenerator`。读取了以下固定版本的实现；README 与许可证阅读沿用 [双窗口检索记录](editor-dual-window-github.md)，不重复引入依赖。

| 候选 | 实际阅读与授权 | 本轮取舍 |
| --- | --- | --- |
| [UnityCsReference](https://github.com/Unity-Technologies/UnityCsReference/tree/bf6028387a4fc2ea6d41aa7d8998e774655aedf6) | PreviewRenderUtility.cs：两盏预览灯的初始化、颜色与启用状态、Render 中环境光覆盖与恢复流程；Reference-Only，仅调用公开接口，不复制实现。 | 继续使用既有预览场景与公开灯光接口。显式设置中性主光和补光，沿用当前独立 Camera.Render 路径，避免在没有 Begin/EndPreview 配对时引入全局环境光覆盖。 |
| [UnityRuntimePreviewGenerator](https://github.com/yasirkula/UnityRuntimePreviewGenerator/tree/87cbffc2d4457860199a5de1fae547315062f41a) | RuntimePreviewGenerator.cs：CameraSetup、独立相机、位置/图层/相机状态保存恢复；README 与 LICENSE.txt（MIT）已在双窗口研究中阅读。 | 主要面向缩略图，不能直接满足持续双窗口；继续参考预览资源隔离，不引入源码或包。 |

两者均不能替代本项目已实现的确认布局预览，且当前需求只涉及编辑器外观。最终复用工程已经使用的 Built-in Standard Shader 和公开调色板基础颜色，创建预览独占材质；不读取真值，也不复用可写的源材质实例。无新增第三方代码，运行时及 Android 路径不增加依赖。

## 修正与验证

原预览把积木、底板和网格全部用 Unlit/Color 重建，灯光不参与积木表面表现。改为只让积木使用非金属、低光滑度的受光材质；底板/网格和投影维持原有显示方式，不降低投影自身亮度。

验证使用实际预览渲染：关闭预览投影后，采样同一蓝色积木的顶面和侧面中心，检查亮度存在可见差异，保存图像作视觉复核；同时运行已有真实场景检查，确认两种模式、实时编辑、投影镜像和源 RGB 不变。所有新增材质在预览释放时清理。

## 实测结果

- 原 Unlit 材质的顶/侧面 RGB 加权亮度差为 0，检查失败。单独替换受光材质后为 0.0193，原预览灯光仍偏暗；加入中性主补光后为 0.183，超过显示回归阈值 0.04。
- Unity 6000.3.24f1 的完整场景检查 179 项通过，原始 RGB 在预览前后变化为 0 像素，无投影采集对照也为 0 像素差异。材质/纯投影/实时编辑截图已目视检查，投影图案本身未改色或降亮。
- 结果：`.scratch/dual-window/run-20261029-172644-289/verification.json`；纯材质图 `plastic-material-probe.png`、完整效果 `live-edited-combined.png`。原 Unlit 完整效果保留于 `.scratch/dual-window/material-before/` 供本地比较。
- 本次只修改 Editor 预览、场景回归检查与说明，无新增运行时依赖或材质资产，不改变源调色板及识别设置。
