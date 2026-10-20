# 实拍拒绝与照片界面遮挡：GitHub 核对

日期：2026-10-09。状态：修复前检索、相关完整实现阅读、候选比较与有限适配方案已完成；修复与验证已完成。

## 范围与现有证据

本次处理用户截图中的 `BRICK_SPLIT_UNCERTAIN`、`BRICK_CROPPED` 和三维地图透过照片界面的问题。颜色角色继续由 Unity 的 Color setup 决定，不修改当前 Core、Wall、Tower 的含义。

从本地扫描记录获知：完整原 JPG 为 1080×2449，用户实际色样为 Background `(65,61,63)`、Core `(251,251,251)`、Wall `(253,97,48)`、Tower `(5,195,254)`，曾返回 `BRICK_CROPPED`；截图对应后续导入的 `qq_pic_merged_1791536977046.jpg` 为 958×938，画面本身截断对象，返回 `BRICK_SPLIT_UNCERTAIN` 与两项 `BRICK_CROPPED`。完整照片中的背景明度变化是否进入未知色前景，需使用原 RGB 与同一 palette 的最小回放确认；不能仅凭截图或旧样本通过结果作结论。后者真实裁切仍应拒绝，不能用忽略全部边缘组件来消除报错。

验收：完整原图在用户实际 palette 下稳定通过并保留独立对象计数；真实裁切照片保持拒绝并指出需要完整入镜；未知颜色物体和确实无法可靠拆分的组件仍拒绝；照片界面覆盖整个窗口时地图不透出，按钮、颜色采样、结果核对与返回行为保留。既有无格纸/格纸样本、相邻积木、模式切换和应用流程回归继续通过。

## 检索与实际阅读

关键词：`site:github.com lego-detector black background contour boundary`、`site:github.com/opencv/opencv threshold_inRange connected components`、`site:github.com/Unity-Technologies/UnityCsReference GUI DrawTexture GUI color`。复用 [无标记入口研究](markerless-photo-github.md) 的候选，重新读取固定提交的 README、许可和完整相关源文件。网络页面部分缓存失败后通过 GitHub raw 原始文件完成读取，没有用搜索摘要替代源文件。

| 候选与读取版本 | 实际阅读与授权 | 适用部分、差异与选择 |
| --- | --- | --- |
| [thomas-rooty/lego-detector](https://github.com/thomas-rooty/lego-detector/tree/209eb05aaf6525da2736a59aeecb97fed8370c82)，提交 `209eb05aaf6525da2736a59aeecb97fed8370c82` | 完整 README、`lego_detector.py`、MIT LICENSE。固定版来自 2023-11-15，沿用旧版机制，不据此宣称当前维护活跃。 | 黑底差分、颜色前景、外轮廓、面积与包围框过滤。需要单独背景照片，原仓库会直接排除触边轮廓；本项目只参考分阶段检查，真实触边物体需要拒绝，不照搬忽略策略。[源文件](https://github.com/thomas-rooty/lego-detector/blob/209eb05aaf6525da2736a59aeecb97fed8370c82/lego_detector.py)、[许可](https://github.com/thomas-rooty/lego-detector/blob/209eb05aaf6525da2736a59aeecb97fed8370c82/LICENSE) |
| [opencv/opencv](https://github.com/opencv/opencv/tree/49486f61fb25722cbcf586b7f4320921d46fb38e)，4.12.0 固定提交 `49486f61fb25722cbcf586b7f4320921d46fb38e` | 完整 README、Apache-2.0 LICENSE；完整 `threshold_inRange.py`、`samples/cpp/connected_components.cpp`、`imageSegmentation.py`，以及 HSV 教程。该固定版不是“最新版本”声明。 | 参考把颜色与明度分开产生掩码，以及先掩码后连通组件的检查。距离分割需要可靠前景种子，无法从裁掉的图像恢复积木，更不能保证同色组件正确块数。沿用现有 C# 连通和单位尺寸拆分。[颜色示例](https://github.com/opencv/opencv/blob/49486f61fb25722cbcf586b7f4320921d46fb38e/samples/python/tutorial_code/imgProc/threshold_inRange/threshold_inRange.py)、[连通示例](https://github.com/opencv/opencv/blob/49486f61fb25722cbcf586b7f4320921d46fb38e/samples/cpp/connected_components.cpp)、[分割示例](https://github.com/opencv/opencv/blob/49486f61fb25722cbcf586b7f4320921d46fb38e/samples/python/tutorial_code/ImgTrans/distance_transformation/imageSegmentation.py)、[许可](https://github.com/opencv/opencv/blob/49486f61fb25722cbcf586b7f4320921d46fb38e/LICENSE) |
| [Unity-Technologies/UnityCsReference](https://github.com/Unity-Technologies/UnityCsReference/tree/cf91ce104c595f52c14d0dea24f56b76c96cdc1a)，提交 `cf91ce104c595f52c14d0dea24f56b76c96cdc1a`，2026-10-08 | README、LICENSE.md；完整 `GUI.DrawTexture` 重载及最终绘制方法（GUI.cs 第 606–845 行）。源码为 Unity 7000.0.0a8 参考版，与本项目 6000.3.24f1 不同，不声称版本完全一致。 | 官方方法显示 GUI.color 传入纹理绘制，并由 alphaBlend 选择混合/直绘材质；读取以确认不透明底层的 API 用法。Unity Reference Only License 不允许修改或再分发其源代码，本项目仅调用公开 API，不复制或翻译内部实现。[完整相关源码](https://github.com/Unity-Technologies/UnityCsReference/blob/cf91ce104c595f52c14d0dea24f56b76c96cdc1a/Modules/IMGUI/GUI.cs#L606-L845)、[README](https://github.com/Unity-Technologies/UnityCsReference/blob/cf91ce104c595f52c14d0dea24f56b76c96cdc1a/README.md)、[许可入口](https://github.com/Unity-Technologies/UnityCsReference/blob/cf91ce104c595f52c14d0dea24f56b76c96cdc1a/LICENSE.md) |

### 完整阅读得到的边界

- lego-detector 的 `process_image` 将样本与单独背景图作灰度差分、模糊、Otsu 二值化，然后按面积、长宽比与边界过滤轮廓；最后产生框和裁图。它提供组织机制，不提供本项目的颜色角色、整数单位拆分或相对网格。原边缘过滤会遗漏真实边缘对象，不适用于本项目安全确认逻辑。[process_image](https://github.com/thomas-rooty/lego-detector/blob/209eb05aaf6525da2736a59aeecb97fed8370c82/lego_detector.py)
- OpenCV HSV 教程区分色相、饱和度和亮度；完整颜色示例用用户可调范围产生掩码。完整连通示例对二值图标记组件，0 号是背景。二者支持“先确定可靠前景，再统计组件”的方式，没有提供本项目具体背景容差。[HSV 说明](https://github.com/opencv/opencv/blob/49486f61fb25722cbcf586b7f4320921d46fb38e/doc/tutorials/imgproc/threshold_inRange/threshold_inRange.markdown)、[连通示例](https://github.com/opencv/opencv/blob/49486f61fb25722cbcf586b7f4320921d46fb38e/samples/cpp/connected_components.cpp)
- OpenCV 分割示例用距离变换峰生成前景种子，再分水岭分区；对紧贴且没有足够独立峰的方块，峰数量不能替代本项目几何一致性检查。[完整分割源文件](https://github.com/opencv/opencv/blob/49486f61fb25722cbcf586b7f4320921d46fb38e/samples/python/tutorial_code/ImgTrans/distance_transformation/imageSegmentation.py)
- Unity 的 DrawTexture 默认启用 alphaBlend，最终使用调用色彩；简单纹理可选择直绘材质。需要在所有控件前绘制真正不透明的全窗口底层并恢复 GUI 状态，不能仅依赖半透明皮肤 Box 遮挡三维地图。[GUI.DrawTexture](https://github.com/Unity-Technologies/UnityCsReference/blob/cf91ce104c595f52c14d0dea24f56b76c96cdc1a/Modules/IMGUI/GUI.cs#L606-L845)、[项目 Unity 版本的公开 API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/GUI.DrawTexture.html)

## 本次有限适配方案（编码前确定）

1. 建立 RGB、尺寸、分析模式与完整用户 palette 的公开识别回放，对照完整原 JPG 与真正裁切 JPG；不以文件名或预设物体位置生成结果。
2. 保留现有 ColorCalibration、NormalizedLab、C# 连通组件和单位尺度拆分。参考 HSV 将色彩与亮度分开判断的机制，区分已标定背景的亮度变化与真正不同的颜色；白色等低饱和积木仍需高置信前景条件，不能把亮背景一概当白色块，或把所有低饱和对象一概当背景。具体阈值由回放与反例验证确定，不复制候选硬编码阈值。
3. 未标定颜色组件仍接受几何、覆盖和边界检查；抑制与背景色彩一致、仅亮度不同的假候选，不无条件放弃边缘对象。真实截断、未知独立积木、非矩形组件覆盖无法可靠拆分时仍返回拒绝。
4. 拆分保持逐单元颜色掩码覆盖验证；L 形的包围框空格不能填满。仅在可证明是边界对象截断时优先解释裁切，不用整体降低 BRICK_SPLIT_UNCERTAIN 阈值。
5. 照片模式在所有控件前使用 Unity 公开绘制 API 覆盖不透明底层，保持 GUI 色彩、矩阵和绘制顺序；在用户截图的 862×678 小窗口验证可读性与返回地图操作。无需引入 uGUI 迁移或额外 UI 库。

上述背景判别和拒绝次序是基于源码机制、项目合同和待验证回放作出的本项目适配判断，不是候选已经实现了本照片的识别。实施时必须记录实际采用方式与测试结果。

## 兼容性、依赖与授权

lego-detector 是 Python/OpenCV/Flask，另含 TensorFlow 类型预测；本项目没有空背景参考图，不直接引入其服务与模型。OpenCV 原生库能离线计算，但 Unity/C# 绑定和 Android 平台包有成本，本次只借鉴算法机制，仍在现有纯 C# Vision 完成。Unity 参考源码仅用于核对公开 API 的绘制行为，与安装编辑器版本存在差异，需由 Unity 实际图像验证补足。

本次为思路参考与现有实现局部适配，不复制、翻译候选源码，也不新增直接运行依赖；因此不需要新增采用源码的第三方许可条目。若实施变为源码复制或翻译，必须重新核对授权，保留相关版权、许可与修改说明；Unity Reference Only 代码不可复制/修改/再分发。[MIT 条款](https://github.com/thomas-rooty/lego-detector/blob/209eb05aaf6525da2736a59aeecb97fed8370c82/LICENSE)、[OpenCV Apache-2.0](https://github.com/opencv/opencv/blob/49486f61fb25722cbcf586b7f4320921d46fb38e/LICENSE)、[Unity 源码使用限制](https://github.com/Unity-Technologies/UnityCsReference/blob/cf91ce104c595f52c14d0dea24f56b76c96cdc1a/README.md)

## 验证计划

- 完整原 JPG + 用户真实 palette：确认拒绝来自哪个阶段；修复后物体数与实拍参考一致，角色映射不变。
- 958×938 真正裁切 JPG：拒绝、说明完整入镜，不能确认覆盖当前关卡。
- 灰背景亮度变化、不同背景采样位置与白积木；未知颜色立方体及触边未知对象；已知积木被裁切；L 形和 2×2 紧贴组件。
- 原 .NET 回归；Unity 原 JPG 解码、确认与实际地图绘制，以及小窗口照片界面的实际屏幕截图。
- 没有进行的硬件相机与 Android 验证不写成通过。

## 实施与验证结果

### 已确认根因与实际适配

完整原图用用户实际 palette 的直接回放稳定返回 `BRICK_CROPPED`。分割诊断发现原坐标约 `(0,924)`、`705×1527` 的未知颜色组件，它是触边的亮灰背景。只将背景样本从 `(65,61,63)` 换为 `(90,78,84)`，独立尺度锚点 55、64、65 及单位 64 不变，背景假组件消失，恢复 10 块。这是单变量实验得出的本项目因果结论。

实际采用 OpenCV 色彩/亮度分开检查的机制，在现有纯 C# 分类前加入低饱和背景判断。与背景色彩一致的低饱和像素，只有达到已标定低饱和积木的亮度容差时才进入前景：下限取这些样本亮度减 `MaximumColorDistance` 加半个 `MinimumColorMargin` 的最小值；没有低饱和积木时沿用背景相对亮度界限。阈值来自现有可配置容差，没有按照片名称、位置或实物 RGB 分支。白色积木保留绝对明度检查，未标定彩色对象、触边和逐单元 55% 覆盖仍按原规则拒绝。没有采用分水岭，也没有忽略全部边缘轮廓。

实际裁切版继续拒绝，新增可选 `Issue.ImageRegion` 传递原图区域；面板画红框，报告保存 `issues[].region`。无法确认时保持当前关卡。GUI 在控件前调用公开 `DrawTexture` 画不透明底层并恢复色彩/矩阵/深度，沿用 IMGUI；没有复制 Unity 参考源码或引入新依赖。

### 已执行验证

在项目根目录执行，均退出码 0：

```powershell
dotnet run --project Tests/BrickDefense.Checks.csproj -- --replay-scan .scratch/scan-rejection/debug/scan-444a6d9f33ed433db335a9bfa49ba752
dotnet run --project Tests/BrickDefense.Checks.csproj -- --replay-scan .scratch/scan-rejection/debug/scan-d431df17628641cca0444cf315667db1 --expect-rejected
dotnet run --project Tests/BrickDefense.Checks.csproj -- --scan-rejection-checks
dotnet run --project Tests/BrickDefense.Checks.csproj
```

第一项修复后 `Success`、10 块；第二项预期 `Rejected`，仍有拆分不确定和两项裁切。新拒绝回归验证完整原图的 1 白/7 橙/2 蓝、75→180 灰背景渐变、原图区域及当前关卡保留。原 .NET 套件包含未知色、已知触边、L/2×2 紧贴、角色交换、格纸原 PNG 与寻路/流程检查，全部通过。

使用 Unity 6000.3.24f1，隐藏启动 `.scratch/scan-rejection/unity-check` 隔离项目：`-batchmode -executeMethod BrickDefense.Editor.DemoSceneChecks.VerifyExamples` 退出码 0；直接解码原 JPG + 用户真实 palette 得 10 块，确认、Renderer/相机显示、模式/示例切换、原文件保存及裁切拒绝区域 JSON 往返检查全部通过。没有关闭或在用户活动项目上运行批处理。

`BrickDefense.Editor.PhotoOverlayChecks.Verify` 在有实际 Game 视图的隔离编辑器中执行，1110×740 截图的空白间隙 46 个样本范围差由旧版 82 降到 0。再以独立 Game 视图调整到用户截图相同的 862×678，范围差 0，退出码 0。无 Game 视图的批处理首次截图超时、窗口标题因重载改变的首次小窗口辅助检查失败均未计为通过；修正辅助环境后取得真实截图。临时生产分割 Console 输出已移除。

完整复现、日志、界面前后及关卡图片见 [修复规格](../../.scratch/scan-rejection/spec.md)。这次验证覆盖给定照片及必要反例；未做 Android 或相机硬件验收，也不声明任意背景照片的通用准确率。
