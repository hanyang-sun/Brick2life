# 真实照片识别：检索与整合方案

日期：2026-10-08。本文在照片识别功能编码前记录。用户暂无实拍照片；实物颜色、尺寸和阈值未校准。

## 范围与验收

当时依据 `docs/requirements-v1.md` 第 2、3、5、7 节：单张 RGB 俯拍、10×10 格纸、方向固定到纸面标记、逐格空/占与颜色识别、同色紧贴积木独立编号、失败拒绝、地图核对后生成三维关卡。该格纸流程后来被移除；现行照片入口见 `docs/requirements-plain-only-v1.2.md`。

## 检索与实际阅读

关键词：`site:github.com jcmellado js-aruco LICENSE detector findMarkers cv.js`、`site:github.com csharp Aruco pure managed marker detector Unity`、`Unity grid color board homography`。原仓库 README、源码和许可证通过 GitHub/raw/API 阅读；API 核对固定提交。

| 候选及实际阅读文件 | 匹配、依赖与兼容 | 授权及选择 |
| --- | --- | --- |
| [jcmellado/js-aruco](https://github.com/jcmellado/js-aruco)：[README](https://github.com/jcmellado/js-aruco/blob/2203d4b5efb601c39054a341bf04702409699383/README.md)、[src/aruco.js](https://github.com/jcmellado/js-aruco/blob/2203d4b5efb601c39054a341bf04702409699383/src/aruco.js)、[src/cv.js](https://github.com/jcmellado/js-aruco/blob/2203d4b5efb601c39054a341bf04702409699383/src/cv.js)、[LICENSE.txt](https://github.com/jcmellado/js-aruco/blob/2203d4b5efb601c39054a341bf04702409699383/LICENSE.txt) | 纯 JS 方形编码标记检测，7×7 标记/内部 5×5 编码；源码把候选四边形、投影采样、解码分开。没有 C# 与 Unity 接口，需小范围移植；不包含格纸逐格颜色判断。提交为 2024-03-28。 | `aruco.js` 顶部为 Copyright (c) 2011 Juan Mellado、完整 MIT 条款。**局部改编 getMarker、hammingDistance、mat2id、rotate 的编码与解码机制**为纯 C#；保留 MIT 声明及仓库完整 LICENSE.txt 附带声明。不采用 cv.js、POSIT、StackBlur 或 AForge 实现。 |
| [evanw/glfx.js](https://github.com/evanw/glfx.js)：[README](https://github.com/evanw/glfx.js/blob/86ae72acfffcd50774507eb7b69887ba2493dc35/README.md)、[perspective.js](https://github.com/evanw/glfx.js/blob/86ae72acfffcd50774507eb7b69887ba2493dc35/src/filters/warp/perspective.js)、[matrix.js](https://github.com/evanw/glfx.js/blob/86ae72acfffcd50774507eb7b69887ba2493dc35/src/core/matrix.js)、[LICENSE](https://github.com/evanw/glfx.js/blob/86ae72acfffcd50774507eb7b69887ba2493dc35/LICENSE) | 四点透视映射可对应格纸定位。完整库依赖 WebGL/JS，不适合 Unity 离线直接接入。 | 仓库 MIT；matrix.js 注明来源 javax.media.jai.PerspectiveTransform。**只参考四点投影的接口和映射流程，不复制矩阵公式实现或引入 WebGL**。本项目使用通用线性方程求解衔接所需坐标。 |

`js-aruco/LICENSE.txt` 同时包含 ArUco BSD、OpenCV BSD、AForge LGPL 和 StackBlur MIT 附带说明。因此本次限制移植范围到有独立 MIT 文件头的 `aruco.js` 解码部分，不把整仓库所有实现视为统一 MIT。

颜色识别参考 [OpenCV 官方 HSV 教程](https://docs.opencv.org/4.x/df/d9d/tutorial_py_colorspaces.html) 的色彩空间/阈值机制；不引入 OpenCV 源码、二进制或 Python 服务。标记定位的候选/解码机制也与 [官方 ArUco 检测说明](https://docs.opencv.org/4.x/d5/dae/tutorial_aruco_detection.html) 比较。

## 具体有限整合

1. 纯 C# `ArucoCode` 放到 ThirdParty，保留完整 MIT，记录源提交与改编范围。只识别四个本项目选定的原版编码 ID；不使用上游 3D 姿态估计。
2. 配套格纸采用固定相对尺寸：14×14 单位纸面，10×10 格网位于 (2,2)…(12,12)，四个标记中心为 (1,1)/(13,1)/(13,13)/(1,13)。每格一个单位，打印时必须填实测积木边长；不能把未知实物尺寸填成默认事实。
3. 参考 js-aruco 的候选方形 → 透视归一采样 → 校验黑边与旋转解码流程。项目适配层从黑色连通区的凸包取得四边形，按四角 ID 建立纸面坐标，检查全部标记和投影一致性。缺失/重复/过小/畸形标记拒绝，不能用图像边缘代替格纸四角。
4. 每格在投影后的内部区域采样，以标定的纸面和三色 HSV 距离、胜出比例及颜色歧义判断空/占/不确定。每个占格独立编号；混色、未知颜色或低可信度拒绝。相关阈值是开发配置，需实物校准，不能宣称跨格与光照容差已达标。
5. `ScanResult` 补纸面观察、照片证据和置信分数。Unity PNG/JPG 导入或 WebCamTexture 快照提供 RGB 像素；用户先核对只读数字地图再确认生成关卡。角色映射由保存的固定标定配置提供；原照片留存供检查。

## Unity 接口依据与验证

已阅读 Unity 官方 [LoadImage](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ImageConversion.LoadImage.html)、[WebCamTexture](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/WebCamTexture.html)、[GetPixels32](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/WebCamTexture.GetPixels32.html)、[videoRotationAngle](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/WebCamTexture-videoRotationAngle.html)、[videoVerticallyMirrored](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/WebCamTexture-videoVerticallyMirrored.html)。采集适配要处理 Unity 像素的底部起始排列、摄像头旋转/镜像和 Android 相机权限。

推断：纯 C# 检测层无平台与网络依赖，可作为 Android 离线识别层；最终仍需真机确认性能及摄像头行为。本机只有 Windows Unity 平台模块，不能据编辑器编译宣称 Android 已验收。

验证采用带标记的合成 RGB 像素、旋转/投影/缺标记/邻接同色/混色/未知颜色等用例，另验证 Unity 的 PNG 解码到关卡流程和已有 200 个寻路布局。合成用例不是实拍准确率数据。

## 实际采用与验证结果

- 实际局部移植仅 `Assets/ThirdParty/JsAruco/ArucoCode.cs`，编码/解码与版权范围见 `THIRD_PARTY_NOTICES.md`；原版完整 LICENSE.txt 已随文件保存。
- 已完成纯 C# 格纸定位、每格投影采样与 HSV 标定识别；扫描数据补纸面变换、按纸面方向排序的四角、帧证据、格位和颜色一致分数。分数是开发用一致性指标，未校准为正确概率。
- Unity 演示已加入 PNG/JPG 本地导入、摄像头快照、旋转/镜像处理、方形取景引导、颜色配置保存、只读数字地图确认和三维关卡生成。分析在后台线程运行，扫描原图与诊断数据保存到本地；未加入云识别或 Android 相册选择器。
- `Brick Defense/Export Scan Board` 已加入，要求输入实测毫米边长，再导出原尺寸 SVG。格纸尺寸及标记位置与定位器一致。
- .NET 检查通过：独立生成的像素图 → 100 格/9 积木 → 400 游戏格/36 占格 → 16 步；90/180/270 度旋转、独立透视投影、相邻同色、缺标记、未知颜色、混色、缺标定均符合预期。既有 200 个布局仍与独立 BFS 一致。
- Unity 6000.3.24f1 最终批处理退出码 0，脚本编译成功；`VerifyPhotoIntegration` 的 PNG 实际解码、像素方向、9 积木和 16 步关卡检查通过，新脚本 `.meta` 已生成。
- 合成图保存在 `Tests/Fixtures/synthetic-reference.png`，已目视核对。当时未获得实拍照片，也未做实物准确率、人工相机交互或 Android 真机验收；后续改为无格纸实拍流程，见 `docs/research/markerless-photo-github.md` 与 `docs/research/plain-only-photo-github.md`。
