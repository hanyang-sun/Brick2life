# Brick2Life 实物桌面投影阅读指南

资料核对日期为 2026-11-09。项目当前已验证固定棋盘、单层积木、RGB 识别与投影反馈的 Unity 仿真。下面的步骤用于首次接入 GF100 摄像头和投影模组后的实机试验。项目已有的仿真验证范围见[固定投影仪仿真](../physical-projector-guide.md)和[验证摘要](../verification/current-simulation.md)。

## 论文与实现资料

1. **MagicBLOCKS，桌面交互系统实例。** Wu 等人在 ACM ISS 2022 论文第 3.4 节和图 2 说明如何用普通 Logitech 摄像头采集桌面、用 OpenCV 识别彩色教具、用投影仪输出 Unity 界面。桌面也划分为 10×10 格。它的 Airbar 用于额外的触摸输入。论文全文入口在项目页的 Publications 下。[Augmented Human Lab 项目页及论文](https://ahlab.org/projects/magicblocks/)
2. **Jin、Seo 与 Han（2026），固定设备下的投影校正。** 先读第 3 节和图 2、图 4。论文先对齐参考平面，再利用彩色标记为具体表面求单应矩阵。适合用来理解为什么棋盘和积木顶面应各自校正。论文的实验对象是静止或准静止的建筑模型。[Journal of Computational Design and Engineering 开放全文](https://doi.org/10.1093/jcde/qwag009)
3. **OpenCV，四点对应的工程实现。** `getPerspectiveTransform` 用四对坐标求 3×3 透视变换矩阵。矩阵可用于变换点，`warpPerspective` 则用于整张图像的透视校正。阅读文档中 `warpPerspective` 的矩阵方向和 `WARP_INVERSE_MAP` 说明，避免把棋盘到投影像素的映射方向用反。[OpenCV 几何图像变换](https://docs.opencv.org/4.x/da/d54/group__imgproc__transform.html)
4. **Huang 等（ISMAR Adjunct 2018），相机与投影仪联合标定。** 看图 1、引言和方法，重点是如何让相机拍到已知投影图案，从而建立投影像素对应关系，并把投影仪按反向相机建模。需要统一处理多种高度或立面时，可参考这套内外参标定方法。[作者开放全文](https://arxiv.org/abs/1803.09058)
5. **Unity 6.3，多显示器输出。** 查看 `Display.displays`、`Display.Activate()` 和渲染相机的 `Target Display`。如果投影模组的驱动板可被 Windows 识别为 HDMI 显示器，可以在扩展桌面模式下用它显示校正后的反馈画面。[Unity Multi-display](https://docs.unity.com/en-us/engine/6000.3/manual/cameras/multiple/multi-display)
6. **Soomro、Ulusoy 与 Urey（2017），分时投影与采集。** 读图 3 的同步设计，理解如何在压低投影贡献的时段采集识别帧。论文把相机滚动快门与 MEMS 激光扫描投影的时序配合。GF100 和 HDMI 模组可先用步骤 4 的较长黑场做基准试验。[IS&T 出版方 PDF](https://library.imaging.org/admin/apis/public/api/ist/website/downloadArticle/ei/29/3/art00010)

## 实机原型顺序

1. **打通输入和输出。** GF100 通过 USB 接入 Windows，先确认实际枚举的分辨率和帧率，再用 Unity `WebCamTexture` 读取新帧。投影模组用带 HDMI 输入的驱动板接入电脑，独立输出四角标记。选定分辨率后固定相机、投影仪和棋盘的位置，以及投影缩放和梯形校正等设置。[Unity WebCamTexture](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/webcamtexture)
2. **分别标定采集和投影。** 给棋盘定义固定的原点和行列方向，量取四个相距较远的同平面标记，用相机图像中的像素坐标求相机像素到棋盘坐标的映射。再投射四个坐标已知且可区分的光点，用相机拍到光点并换算成棋盘坐标，据此求棋盘坐标到投影像素的映射。先用道路和格框验证棋盘面上的对齐。10×10 棋盘若采用 2.5 cm 格距，有效区为 25×25 cm，映射和误差换算应使用现场实测尺寸。
3. **分开处理棋盘和积木顶面。** 约 1.9–2.0 cm 的单层积木顶面相对棋盘有高度差。先用垫至实测高度的平面校正顶面图案，保留地面和顶面两组投影映射。若识别取样点落在积木顶面，也应按相应平面映射检查其格位。用未参与求解的边缘及内部测点检查落点误差，按毫米报告地面和顶面各自的结果。若画面边缘仍有系统性偏差，先检查 GF100 镜头实际畸变，再决定是否去畸变。
4. **最后验证识别受投影影响的程度。** 先测固定投黑与正常播放两种状态下的空格和积木颜色识别。若投影使颜色混淆，先用较长的黑场时段采图，记录显示切换后多久能取得亮度稳定的新帧，再缩短黑场。Unity 已输出黑色的时刻、投影模组实际变黑的时刻和相机曝光完成的时刻需要分别实测。

两组平面映射适用于固定设备和已知高度的平整表面。多层积木或侧面需要额外的表面高度或三维几何信息。投影光路被积木或手遮住时，落点校正无法照亮被挡住的位置。模组黑场残余亮度、积木表面反射、GF100 实际图像畸变和端到端延迟均需以实机数据评估。
