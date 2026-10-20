# 无标记实拍识别：GitHub 检索与有限借鉴

日期：2026-10-09。状态：编码前检索、有限适配和本次样本验证已完成。

## 任务范围与验收

用户要求直接识别 `D:/下载/1791530226265.jpg` 一类实拍：相近尺寸立方体、单层摆放、黑灰背景，没有格纸或四角标记。照片可见 7 个橙色方块、2 个蓝色方块、1 个白色方块，包括紧贴的同色方块。此计数用于该样本的验收，颜色所代表的游戏角色由用户在 Unity 的 Color setup 中采样决定，识别算法不可预设白色、橙色、蓝色的角色。

原 `docs/requirements-v1.md` 限定有方向标记的 10×10 格纸，明确排除自由摆放；本次是用户随后授权的新入口。无标记照片不能确定纸张原点、空格总范围、真实格位或世界毫米尺度。新入口根据积木像素边长和相对位置推断离散布局，归一到 10×10 预览，再沿用 2×2 游戏格映射；界面和报告必须标明相对布局，不能把它称为恢复了真实固定格纸。既有有标记流程及其定位失败处理保留。

验收：原 JPG 经真实导入、用户色样标定、新入口分析、确认与关卡绘制；该照片识别 10 个独立方块，同色紧贴仍独立编号。检验角色重新标定、噪点、光照、组件几何或网格越界的拒绝；既有有标记照片、旋转、两个示例和切换回归继续通过。Android 离线可运行是设计约束，不能把未进行的真机测量写成通过。

## 检索与实际阅读

检索关键词：`site:github.com lego detection black background opencv`、`site:github.com lego color detection HSV white`。搜索发现候选后读取仓库所有者提供的 README、完整相关函数/类和授权文件；不是只根据搜索摘要做选择。版本及提交日期经 GitHub API 核实。

| 候选与固定读取版本 | 实际阅读、授权、维护 | 功能匹配与决定 |
| --- | --- | --- |
| [thomas-rooty/lego-detector](https://github.com/thomas-rooty/lego-detector/tree/209eb05aaf6525da2736a59aeecb97fed8370c82)，提交 `209eb05aaf6525da2736a59aeecb97fed8370c82`，2023-11-15 | README、完整 `lego_detector.py`（134 行）、`requirements.txt`、MIT LICENSE，版权 Thomas Caron 2023。该 HEAD 日期较旧，没有以活跃维护为选择依据。 | 黑底定位积木，过滤小面积与触边对象后输出框和裁图。采用掩码、连通对象、几何过滤这一组织机制；本项目无空背景参考照片，不能直接使用其背景差分流程。Python Flask/TensorFlow 栈不引入。 |
| [afonsotmm/computer-vision-lego-kit-assembly](https://github.com/afonsotmm/computer-vision-lego-kit-assembly/tree/610cc7fbfa1bc3b67e1165f927776488cd41b54c)，提交 `610cc7fbfa1bc3b67e1165f927776488cd41b54c`，2025-10-25 | README；Notebook 代码中完整 `Image`、`BoxShape`、`MultiFormat`、`UnitAwareDimension` 与白色识别方法。GitHub 全树仅 README、PDF、Notebook，无 LICENSE，根页也未声明授权；未确认复制/改编权限，只作算法思路参考。课程项目，无维护承诺。 | HSV 分类、掩码去噪、对象尺寸和形心符合局部需求。使用低饱和处理的思路，颜色仍由本项目用户标定。白色管线依赖凸点 Hough 与 DBSCAN，实拍立方体没有凸点，放弃该管线。 |
| [opencv/opencv](https://github.com/opencv/opencv/tree/49486f61fb25722cbcf586b7f4320921d46fb38e)，固定 tag `4.12.0`，提交 `49486f61fb25722cbcf586b7f4320921d46fb38e`，2025-07-02 | README、Apache-2.0 LICENSE；完整 `threshold_inRange.py`、`imageSegmentation.py`、`watershed.py`；官方颜色阈值、腐蚀膨胀与距离分割教程。选固定读取版，不宣称它是当前最新版本。 | 提供分割与接触对象处理的可靠算法依据。参考颜色掩码和形态去噪；评估距离变换/分水岭，但该照片紧贴矩形可能没有独立峰，不能仅凭峰数算块数，优先单位边长几何拆分。此次不直接引入 OpenCV 本体或复制示例源码。 |

### 完整源码阅读所得

MIT 候选的 `process_image` 依赖一张背景照片，灰度差分、模糊、Otsu 阈值、外轮廓与包围框完成检测；小面积与触边轮廓剔除，再裁图。它处理对象框，不保证紧贴方块按数量拆开，也不输出网格。[完整源文件](https://github.com/thomas-rooty/lego-detector/blob/209eb05aaf6525da2736a59aeecb97fed8370c82/lego_detector.py)

Notebook 的 `Image.get_masks/get_contours/build_bricks` 是彩色管线，`BoxShape` 提供旋转框和形心；白色另经低饱和掩码、Hough 凸点与 DBSCAN 聚类。其尺寸来自外部标定，不能移作本照片的真实毫米值。[完整 Notebook](https://github.com/afonsotmm/computer-vision-lego-kit-assembly/blob/610cc7fbfa1bc3b67e1165f927776488cd41b54c/lego_kit_assembly.ipynb)

OpenCV 的阈值示例把图像转 HSV 后按可调范围产生掩码；距离分割示例经二值掩码、距离变换、阈值峰、轮廓种子和分水岭分开对象；交互示例展示人工种子。它们不包含本项目的自动格位推断或角色映射。[颜色完整示例](https://github.com/opencv/opencv/blob/4.12.0/samples/python/tutorial_code/imgProc/threshold_inRange/threshold_inRange.py)、[距离分割完整示例](https://github.com/opencv/opencv/blob/4.12.0/samples/python/tutorial_code/ImgTrans/distance_transformation/imageSegmentation.py)、[交互完整示例](https://github.com/opencv/opencv/blob/4.12.0/samples/python/watershed.py)

## 兼容性与依赖成本

| 候选 | Unity/C# 接入 | Android 离线适配 | 此次引入方式 |
| --- | --- | --- | --- |
| lego-detector | Python NumPy/OpenCV 与 Flask API 不能直接编入当前纯 C# Vision。类型预测还依赖 TensorFlow。 | 原仓库面向 Python 服务；直接使用需要服务或另一套运行时，未提供本项目 Android 集成。 | 仅读机制；用当前 RGB/颜色分类与 C# 连通搜索作局部适配。 |
| lego-kit-assembly | Python Notebook、Colab、NumPy、Pandas、Matplotlib、scikit-learn；其代码授权未确认。 | 未提供 Unity Android 构建；Colab 或桌面依赖不满足当前集成方式。 | 只借鉴颜色与几何分析思路，不复制代码或固定颜色阈值。 |
| OpenCV | 官方示例为 Python/C++。完整库需 Unity C# 绑定、原生库和平台打包。 | 原生算法能离线运算，但接入及设备验证有额外成本，当前项目没有该运行时依赖。 | 仅读算法；复用既有 C# 分类与连通模块，保持 Vision 不依赖 UnityEngine。 |

以上是根据源码与项目结构作出的兼容性判断，不是候选在 Unity/Android 实测结果。[MIT 许可](https://github.com/thomas-rooty/lego-detector/blob/209eb05aaf6525da2736a59aeecb97fed8370c82/LICENSE)、[第二候选全树](https://github.com/afonsotmm/computer-vision-lego-kit-assembly/tree/610cc7fbfa1bc3b67e1165f927776488cd41b54c)、[OpenCV Apache-2.0 许可](https://github.com/opencv/opencv/blob/4.12.0/LICENSE)

## 编码前确定的有限借鉴方案

1. 增加显式 `Board paper / Plain background` 两种入口。无标记入口由用户选择；有标记模式的缺失、部分标记或定位错误仍返回原拒绝原因，不自动降级为自由摆放。
2. Color setup 沿用用户采样：物理颜色分类依赖当前 ColorCalibration，角色关系在 LevelRules 中。两种入口使用独立标定配置，保护已有格纸的色样；四个采样包括背景与三种角色的代表色，换颜色/角色后通过公开分析流程重算。不得把该样本的白、橙、蓝角色常量写进算法。
3. 思想参考三个候选的颜色掩码和几何过滤，整合本项目已有 RGB/NormalizedLab 分类、连通搜索与 ScanResult 合同；饱和颜色与低饱和浅色分支都要对背景做区分，避免灰黑桌面和阴影变成积木。
4. 小范围去噪后按颜色连通区域统计面积、框与形心。以可靠近方形区域和组件较短边估计统一像素边长，避免将整个 3 连块当作一块，也避免以单个噪点估尺度。
5. 对同色矩形组件按边长估计整数行列，用实际掩码覆盖/几何一致性验证后逐单元生成独立 BrickObservation。L 形和 2×2 等组件必须按覆盖实际拆分；不能仅按最大包围框填满。若旋转、阴影、遮挡或尺度估计不足以可靠拆分，返回原因。
6. 依据照片方向、单元中心相对距离生成相对离散10×10位置，保留 ImageEvidence 中照片测量位置。保证无重复格、布局不超界；报告包含入口来源和尺度推断信息，以区别有标记的 BoardObservation。
7. 沿用 PhotoScanSession 的分析快照、ConfirmLayout 与 LevelSession 的整体替换。拒绝扫描不能覆盖当前关卡。文件读取、PlayerPrefs 标定和报告保存继续由 Infrastructure 负责。

采用方式为**机制参考与现有 C# 实现的局部复用**，本次研究不直接依赖或改编上述仓库源码。因此研究本身不新增第三方代码授权条目；若实施中改为复制/翻译源码，须先保留源许可、版权与修改声明，并更新 THIRD_PARTY_NOTICES.md。无授权 Notebook 不得复制/翻译源码。

算法标准依据：[OpenCV 颜色范围](https://docs.opencv.org/4.12.0/da/d97/tutorial_threshold_inRange.html)、[腐蚀与膨胀](https://docs.opencv.org/4.12.0/db/df6/tutorial_erosion_dilatation.html)、[距离变换与分水岭](https://docs.opencv.org/4.12.0/d2/dbd/tutorial_distance_transform.html)。相对布局生成属于本项目基于单元尺度的衔接，不是这些候选已提供的格纸恢复功能。

## 实际实施与差异

- 新增纯 C# `PlainBackgroundRecognizer`，共用 CaptureSet、ColorCalibration、ScanResult、NormalizedLab；没有引入 Python、OpenCV、Flask、模型或网络调用作为游戏运行依赖。
- 用已标定的色彩距离产生掩码；低饱和样本额外保留绝对明度，避免白积木与灰黑背景被明暗归一化合并。局部同色邻居多数过滤噪点，四邻域搜索连通区域。
- 以近方形组件较短边的中位数估计单位尺寸。原实拍白顶面较小、蓝方块可见侧面，取中位数而非最小边长通过实际几何检查；没有靠固定文件名、RGB 值或预设布局输出结果。
- 对组件包围范围估计整行整列，每个单元逐像素检查本组件颜色覆盖；低覆盖空单元保留为空。实拍中的三连块和两连块、独立合成的 L 形与 2×2 均拆成独立对象。不直接复制候选源码或其硬编码颜色范围。
- 明确 `Board paper / Plain background` 切换及独立 PlayerPrefs 标定键。PhotoScanSession 管理模式和旧结果失效，仍使用分析时标定确认；角色由 Unity Color setup 控件的采样顺序决定。
- Contracts 增加 LayoutCoordinateBasis，报告增加模式与坐标依据。无格纸结果没有标记 ID、纸张角点或可靠实测格纸声明；视图显示“relative photo”，照片预览绘制逐块测量框。
- 原 v1.0 不静默改写，增加 `docs/requirements-markerless-v1.1.md` 说明用户授权的新入口、相对坐标约定与边界。

## 验证结果（2026-10-09）

1. `dotnet run --project Tests/BrickDefense.Checks.csproj` 退出 0：原照片 RGB 检测为 7 橙、2 蓝、1 白，共 10 个独立编号和 90 相对空格；逐块中心与独立人工参考位置对照通过。
2. 半尺寸原图、交换白/蓝的 Core/Tower 采样顺序、L 形/2×2 同色组件、未知绿色物体、裁切对象、空背景与模式切换失效通过。白灰标定可区分，角色更换后位置和物理颜色观测保留。
3. 原有 200 个独立 BFS 对照布局、格纸照片旋转/透视与拒绝、AI 生成原 PNG、颜色及 LevelSession 流程回归全部通过。
4. Unity 6000.3.24f1 隔离项目批量调用 `DemoSceneChecks.VerifyExamples`，退出 0，记录 `PLAIN_REAL_JPG_PASSED` 和 `DEMO_SCENE_CHECKS_PASSED`：直接解码原 JPG、模式入口、标定、公开确认、10 个活跃 Renderer 和相机渲染、返回原示例通过。
5. 原 JPG 报告保存的字节与导入源一致，记录 `PlainBackground`、`RelativeBrickScale`、`boardIsReliable=false` 和测量证据；纯逻辑与 runtime/editor 程序集边界仍通过。

原 JPG SHA-256 为 `042c707dabae705cf305f72527680874bc141aff6e69d88a80a95c2792eb7696`，回归文件未缩放、修改或替换为合成图。详细样本格式见 `Tests/Fixtures/real-unmarked-bricks-v1.md`。日志为 `.scratch/markerless-photo/unity-play.log`，实拍生成地图截图为 `.scratch/markerless-photo/Artifacts/real-unmarked-photograph.png`。

这些结果只验收本次样本及列出的回归。一个原 JPG 正确不能推出通用实拍准确率；硬件相机、Android 性能、严重透视/旋转、堆叠、遮挡和复杂背景须另行验证。单个整片同色矩形且没有可靠独立方块时仍存在单位尺寸歧义。
