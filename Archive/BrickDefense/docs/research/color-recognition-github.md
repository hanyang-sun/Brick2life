# 照片颜色识别改进：GitHub 检索与有限借鉴

检索日期：2026-10-08。范围：用户反馈 `Tests/Fixtures/generated-overhead-bricks-v1.png` 的颜色识别差；本次改进颜色标定、逐格分类及阴影处理，不改变十乘十格纸、方向标记、同色相邻积木独立记录和不确定时拒绝的规则。

## 需求与现状

依据 `docs/requirements-v1.md` 第 2、3、4.1、6 节，可靠区分纸面及三个标定颜色；反光、阴影、颜色不能可靠判定和跨格时继续拒绝。不按图片内容硬编码游戏角色；测试图是带自然纹理的生成图，不能作为实拍或 Android 性能验收。

现有分类使用单点标定的 HSV 加权距离与绝对阈值，格内十乘十采样要求至少 88% 一致。一个颜色的亮面、阴影、侧面和反光会偏离单个标定值；仅放宽所有阈值可能把阴影纸面判成积木，或接受两种混合颜色。需要可追溯的光照处理与区域统计。

关键词：`site:github.com CSharp color CIELAB DeltaE MIT`、`site:github.com illumination invariant normalized rgb color classification MIT`、`sRGBCompanding RGBToXYZ XYZToLab`、`opencv xphoto grayworld_white_balance`。

## 候选比较与实际阅读

| 候选及固定版本 | 实际阅读 | 授权 | 匹配、差异与决定 |
| --- | --- | --- | --- |
| [tompazourek/Colourful](https://github.com/tompazourek/Colourful/tree/9c9a2c80a28b4108fc00f2b87ebc18bf38cea66c)，提交 `9c9a2c80a28b4108fc00f2b87ebc18bf38cea66c` | README、LICENSE、`src/Colourful/Spaces/RGB/Internals/WorkingSpaces/Companding/sRGBCompanding.cs`、`src/Colourful/Spaces/LinearRGB/Internals/LinearRGBToXYZConverter.cs`、`src/Colourful/Spaces/Lab/Internals/XYZToLabConverter.cs` | MIT，Copyright (c) 2023 Tomáš Pažourek；已读完整许可证 | C# 的标准色彩转换，可局部适配 sRGB 逆 gamma 或 D65 XYZ/Lab 变换。README 包含 .NET Standard 2.0/1.1 目标，但最新源码有文件作用域 namespace，原样导入仍需检查 Unity 语言版本。采用少量数值转换，避免整体 converter、矩阵构建及多个色域接口依赖。纯数学、无网络和 native 依赖，适合离线；Android 实际行为仍需打包验证。 |
| [colour-science/colour](https://github.com/colour-science/colour/tree/248121e33fcae458e62190ac872f3e16ab2044bf)，提交 `248121e33fcae458e62190ac872f3e16ab2044bf` | README、LICENSE、`colour/models/cie_lab.py` 的完整 `XYZ_to_Lab` 与 `Lab_to_XYZ` | BSD-3-Clause，Copyright 2013 Colour Developers；已读完整许可证 | D65 Lab 变换含参考输入输出，可作为独立数值检查依据。Python/Numpy 实现不适合作为 Unity 运行时依赖；只参考数值空间与验证方法，不复制 Python 源码。 |
| [opencv/opencv_contrib](https://github.com/opencv/opencv_contrib/tree/4.12.0)，标签 `4.12.0` | README、根 LICENSE、完整 `modules/xphoto/src/grayworld_white_balance.cpp` | 根目录 Apache-2.0；该源文件另外保留 Intel/Willow Garage 的 BSD 风格许可。源码授权不能仅按根目录标签判断。 | GrayworldWB 先排除饱和像素，再按通道总和求增益，明确说明光照校正的已有机制。全图 gray world 会受木桌和彩色积木比例影响；本项目已有纸面标定，应限于已知纸面的中性参考。只借鉴过滤和通道校正机制，不复制 C++、SIMD 或引入 native OpenCV。 |

固定提交通过 GitHub commits API 核实。Colourful 的三份源文件和 colour 的 Lab 源文件使用固定 SHA 的 raw URL 完整读取；OpenCV 使用发布标签读取。除上述实际内容外，没有声称审查完整库或其精度。

## 具体整合方案（编码前确定）

1. 局部适配 Colourful 的 sRGB 数值转换：`V <= 0.04045 ? V / 12.92 : pow((V + 0.055) / 1.055, 2.4)`，以及 XYZ 相对目标白点和 Lab 分段立方根机制。先依据原 RGB 最大通道检查是否过暗，再按最大通道归一化 RGB，以削弱标量亮度；之后转成 D65 Lab，比较 a/b 及较弱的 L 距离。亮度和未知/近色 margin 仍为单独的拒绝依据。此组合是本项目适配，不是 Colourful 自带分类器。
2. 本次保留当前七乘七中位数区域标定；不增加纸面白平衡、多样本配置或中心区域缩小采样。OpenCV 的过滤与白平衡仅为比较过的候选：全图平均会受木桌和彩色积木比例影响，未证实本次有必要引入。暗到没有色彩信息和接近两种颜色的样本继续不确定；降低标量亮度影响不能把纯白纸灰影判为彩色积木。
3. 当前真实图复现发现：格纸定位通过，旧分类仅识别出 9 块中的 5 块，9 格不确定；只放宽颜色距离或只降低一致率都可能让该图通过。因此保留原十乘十格内采样位置（格内 0.08 到 0.92）与 88% 一致率，只修正色彩度量，避免同时放松覆盖要求而无法区分哪一步修复有效。明显两种积木颜色仍须拒绝。
4. 保持 `ColorCalibration` → `PhotoRecognizer` → `ScanResult` 接口及原有格纸定位前置条件；不把 AI 生成图上的外观修正当作自动定位正确的证据。不能可靠定位的输入必须继续给出定位原因，不能改成假定全图就是格纸。
5. 不增加在线 API、云识别、模型下载或每局角色重映射。采用的数学子集只依赖 `System.Math`，测试和 Unity 共用。

直接改编 Colourful 数值代码前，保留完整 MIT 文本并在 `THIRD_PARTY_NOTICES.md` 添加固定提交、适配文件和修改范围。OpenCV 与 colour 在上述方案中仅作机制与数值验证参考，未复制代码。

### 读取的转换常量来源

- Colourful `src/Colourful/Utils/Internals/CIEConstants.cs`：`Epsilon = 216 / 24389`，`Kappa = 24389 / 27`。
- `src/Colourful/Spaces/RGB/RGBWorkingSpaces.cs` 的 sRGB 原色：R `(0.64, 0.33)`、G `(0.30, 0.60)`、B `(0.15, 0.06)`。
- `src/Colourful/Spaces/XYZ/Illuminants.cs` 的 D65：XYZ `(0.95047, 1, 1.08883)`。
- `src/Colourful/Spaces/LinearRGB/Internals/LinearRGBConversionUtils.cs` 的 `GetRGBToXYZMatrix`：按原色 `(x/y, 1, (1-x-y)/y)` 组成矩阵，再由矩阵逆乘白点取得三通道缩放。适配时可将由固定 sRGB 原色和 D65 推导的矩阵固化，不需要引入整个工作色域框架。以上四个文件均完整或对应定义逐项读取，来源为相同固定提交。

## 验证计划

### 逐项探针后的采样调整（2026-10-08，编码前补充）

只更换亮度归一化颜色特征后，原图从 5/9 改善至 8/9，阴影空格不再拒绝。
剩余蓝格 `(3,2)` 的原采样仅 83% 有效：右边和下边存在近黑侧面，另有一排蓝色侧面的色度偏移。
因此采用中心区域 `.20..80` 的 10×10 采样判断主色，仍要求 88% 一致。
同时保留原 `.08..92` 的覆盖检查：另一种积木颜色或有足够亮度但不能分类的像素超过 12%，
或整格主色覆盖低于 65%，继续拒绝，防止中心采样漏过边缘混色、未知色或小块侵入。
置信分数明确表示中心颜色一致率。该布局统计属于本项目接口适配，不复制第三方算法。

- 先用用户指定生成图复现旧版本结果，再检查修改后的纸面与各颜色分类；图像及标定值必须相同，记录定位失败与颜色失败分别发生在哪一步。
- 覆盖深浅阴影、白纸灰影、反光、小片混色、半格两色、未知颜色与低分辨率，检查拒绝理由，不只验证正常样本。
- 原有透视、旋转、同色相邻与关卡寻路检查继续通过；若改编 Lab，用 source 示例或标准色对应值做数值检查。
- .NET 检查与 Unity 编译通过后，报告实测结果；没有实物照片时不能声称实拍识别准确率或 Android 性能已验收。

## 实际适配与验证结果

- 采用 Colourful 的数值子集于 `Assets/ThirdParty/Colourful/NormalizedLab.cs`，保留完整 MIT 授权；未引入 Python、OpenCV、网络或模型依赖。
- 基线：格纸定位成功，5 块识别、9 格不确定，拒绝扫描。
- 单独更换颜色特征：8 块识别、1 格不确定；补充中心采样和外围保护后，原图所有 100 格通过：6 蓝、2 红、1 绿、91 空格。固定标定样本与原 PNG 均未改变。
- 2026-10-08，`dotnet run --project Tests/BrickDefense.Checks.csproj` 退出 0：原图每块颜色/位置、16 步路线、亮度渐变与纸面灰影通过；近黑、半格混色、边缘第二颜色和边缘未知颜色均拒绝。原旋转、透视、缺标记等检查和 200 个 BFS 对照布局通过。
- Unity 6000.3.24f1 隔离项目批处理退出 0，日志包含 `GENERATED_PHOTO_COLOR_PASSED`、`PHOTO_INTEGRATION_PASSED`：Unity 原 PNG 解码、实际照片面板 JSON 设置加载、9 块/91 空格/16 步路线与方向适配通过。当前用户编辑器保持运行。
- 诊断程序归档到 `.scratch/color-recognition/debug/`，产品和正式检查无调试日志。构建缓存及隔离项目已加入忽略规则。
- 生成图与明暗渐变仅为开发回归；真实照片、强反光/色偏、模糊、仅亮度不同的砖色及 Android 真机表现尚未验收。未声称达到实拍准确率。

## 主要源链接

- [Colourful README](https://github.com/tompazourek/Colourful/blob/9c9a2c80a28b4108fc00f2b87ebc18bf38cea66c/README.md)、[MIT 授权](https://github.com/tompazourek/Colourful/blob/9c9a2c80a28b4108fc00f2b87ebc18bf38cea66c/LICENSE)。
- [sRGBCompanding](https://github.com/tompazourek/Colourful/blob/9c9a2c80a28b4108fc00f2b87ebc18bf38cea66c/src/Colourful/Spaces/RGB/Internals/WorkingSpaces/Companding/sRGBCompanding.cs)、[LinearRGBToXYZConverter](https://github.com/tompazourek/Colourful/blob/9c9a2c80a28b4108fc00f2b87ebc18bf38cea66c/src/Colourful/Spaces/LinearRGB/Internals/LinearRGBToXYZConverter.cs)、[XYZToLabConverter](https://github.com/tompazourek/Colourful/blob/9c9a2c80a28b4108fc00f2b87ebc18bf38cea66c/src/Colourful/Spaces/Lab/Internals/XYZToLabConverter.cs)。
- [colour Lab 转换](https://github.com/colour-science/colour/blob/248121e33fcae458e62190ac872f3e16ab2044bf/colour/models/cie_lab.py)、[BSD-3-Clause 授权](https://github.com/colour-science/colour/blob/248121e33fcae458e62190ac872f3e16ab2044bf/LICENSE)。
- [OpenCV Grayworld 实现](https://github.com/opencv/opencv_contrib/blob/4.12.0/modules/xphoto/src/grayworld_white_balance.cpp)、[OpenCV README](https://github.com/opencv/opencv_contrib/blob/4.12.0/README.md)、[根 Apache-2.0 授权](https://github.com/opencv/opencv_contrib/blob/4.12.0/LICENSE)。
