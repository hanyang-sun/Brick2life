# 三维工作台与固定 RGB 小预览：GitHub 检索与有限借鉴

日期：2026-10-16，Asia/Shanghai。记录完成于本步场景配置与辅助代码编写之前。

## 范围与验收

依据当前 `.scratch/task-a-simulation/spec.md` 与用户本轮确认：操作主视图采用正交斜俯视，能看到单位积木顶面和侧面；顶置实验相机继续固定输出原有 512×512 RGB；既有 RawImage 缩到侧栏。先完成固定视图，旋转、缩放、编辑、识别和投影均为后续小步。

验收：主区域完整显示 10×10 棋盘及三色积木，预览保持 1:1；操作相机与实验相机使用独立 Transform、输出和明确的监听归属；在当前主编辑器内实际运行并检查；改变操作相机姿态不改变实验相机图像。保留现有场景对象、物理参数、纹理与 `.meta`。

旧 `docs/requirements-v1.md` 的实体照片塔防规则属于历史需求，本步采用当前仿真规格。

## 检索与实际阅读

关键词：`site:github.com Unity RawImage RenderTexture camera preview uGUI FFmpegOut`。以下候选实际阅读 README、相关源码和许可证；上游 main/master 为本日读取的分支快照，没有取得固定提交号，不将其当作本机 Unity 的实现版本。

| 候选与已读文件 | 匹配、授权、兼容性和维护证据 | 有限采用方案 |
| --- | --- | --- |
| [Unity-Technologies/uGUI](https://github.com/Unity-Technologies/uGUI)：[README](https://github.com/Unity-Technologies/uGUI/blob/main/com.unity.ugui/README.md)、[RawImage.cs](https://github.com/Unity-Technologies/uGUI/blob/main/com.unity.ugui/Runtime/UGUI/UI/Core/RawImage.cs)、[AspectRatioFitter.cs](https://github.com/Unity-Technologies/uGUI/blob/main/com.unity.ugui/Runtime/UGUI/UI/Core/Layout/AspectRatioFitter.cs)、[LICENSE.md](https://github.com/Unity-Technologies/uGUI/blob/main/com.unity.ugui/LICENSE.md)。另读取本机 BuiltInPackages/com.unity.ugui 的 README、LICENSE 和相关源码。 | 官方 Unity UI 项目，Unity Companion License；本机已有 uGUI 2.0.0。RawImage 接收 Texture，FitInParent 按父矩形约束保持比例，适合显示现有 RenderTexture。上游页面存在提交历史，不据此声称某次提交已通过本机测试。源码与本机包无需新增运行时网络或原生插件；Android 离线运行仅作依赖分析，未真机验证。 | 继续直接依赖现有官方组件，不复制源码、不新增包。用锚点把背景及预览限制到侧栏，保留 FitInParent=1。操作相机直接输出到屏幕中的主区域。 |
| [keijiro/FFmpegOut](https://github.com/keijiro/FFmpegOut)：[README](https://github.com/keijiro/FFmpegOut/blob/master/README.md)、[Runtime/CameraCapture.cs](https://github.com/keijiro/FFmpegOut/blob/master/Assets/FFmpegOut/Runtime/CameraCapture.cs)、[LICENSE.md](https://github.com/keijiro/FFmpegOut/blob/master/LICENSE.md)。 | 源码 MIT，README 另说明编码二进制包 GPL；README 限定桌面。源码 Update 中把相机输出重定向到纹理，并用独立 blitter 保留屏幕显示，说明采集与屏幕呈现需要明确分工。README 建议一般录制优先考虑 Unity Recorder；本次未取得最近提交日期，不推断维护频率。 | 仅参考采集输出与屏幕呈现分工。当前只需要双相机显示，编码器、会话及 blitter 无必要，不引入、不复制、不依赖。 |

uGUI 根目录 LICENSE 和 FFmpegOut 的无扩展名 LICENSE 抓取曾失败，之后实际取得的是表中各包的 LICENSE.md；授权结论以已取得正文为依据。

## 场景适配

1. 在 SimulationRoot 下增加 WorkspaceCamera，朝向棋盘中心，初始正交斜俯角参考旧原型的 55°。使用 Built-in Camera、独立 Transform 和直接屏幕输出，不复用实验相机作为操作视图。
2. WorkspaceCamera 占左侧主区域；SimulationCanvas 保持 Screen Space Overlay。PreviewBackground 改为右侧背景，PreviewArea 改成右上预览区域，CameraPreview 仍引用 TopCameraRGB，保持正方形。
3. MainCamera 标签与唯一启用的 AudioListener 归操作相机；TopCamera 改为 Untagged，移除它的监听组件。后续点选显式引用操作相机，采集显式引用实验相机。
4. 初案不增加运行时脚本。读取当前主编辑器后发现现有 Game 区域为 570×720，固定尺寸会裁切棋盘；因此补充下面的编码前比较，增加一个只调整正交尺寸的小脚本。交互旋转、缩放和编辑仍为后续小步。
5. 中文说明沿用项目现有 Noto Sans SC 字体。既有字体和 uGUI 授权声明保持适用，没有新增第三方代码采用。
6. 临时 Editor 辅助脚本只在明确收到本步命令时配置/验证；先导出实际内存状态并备份场景，配置使用 Undo，完成后从 Assets 移到 `.scratch`。不得重新启用历史自动修复诊断脚本，不关闭用户编辑器，不用第二实例打开主工程。

## 验证方法

- 检查当前主编辑器的活动场景、Play 状态、相机和 UI，保留配置前的实际状态及磁盘备份。
- 配置并保存后，在同一个主编辑器实际进入 Play，捕获包含 UI 的 Game 画面以及 TopCameraRGB；检查完整棋盘、三色、预览比例、单一屏幕相机/监听、无丢失脚本。
- 暂时仅改变操作相机姿态并重新拍摄实验纹理，比较像素，之后恢复姿态；不保存验证时的姿态修改。
- 改变 Game 视图尺寸验证预览比例和棋盘入框，结束后恢复用户原来的尺寸设置与 Play 状态。
- 验证结论追加到本记录，并在 `.scratch/task-a-simulation/workspace-view/` 保存实际证据。

## 当前窄窗口取景：补充编码前比较

实际内存与截图：`.scratch/task-a-simulation/workspace-view/before-state.txt`、`before.png`。用户原来处于 Play，配置前先停止运行，结束后恢复运行状态。

继续实际读取 [Unity-Technologies/com.unity.cinemachine](https://github.com/Unity-Technologies/com.unity.cinemachine) 的 [README](https://github.com/Unity-Technologies/com.unity.cinemachine/blob/main/README.md)、[CinemachineGroupFraming.cs](https://github.com/Unity-Technologies/com.unity.cinemachine/blob/main/com.unity.cinemachine/Runtime/Behaviours/CinemachineGroupFraming.cs) 的 OrthoFraming/GetFrameHeight 与 [LICENSE.md](https://github.com/Unity-Technologies/com.unity.cinemachine/blob/main/com.unity.cinemachine/LICENSE.md)。官方维护包，Unity Companion License；源码以相机坐标中的包围范围和 lens.Aspect 求取正交半高，支持水平、垂直或双方向约束。原模块依赖虚拟相机、目标组与阻尼管线，本项目已有真实 Camera 且对象范围固定，引入整个包的成本高于所需功能。不声明上游 main 已在本机通过测试。

与 uGUI 的 FitInParent 比较：FitInParent 只解决显示矩形的比例，不能调整直接输出屏幕的 Camera 视野；FFmpegOut 的纹理和 blitter 分工也不解决相机裁切。因此仅参考 Cinemachine 的“相机空间范围＋宽高比”机制，不复制其源码或新增包。本项目独立编写 SimulationWorkspaceCamera：检查 X/Z=0～10、Y=0～1 的八个角点，把它们转换到操作相机局部坐标，以所需半高和半宽/相机宽高比的较大值确定 orthographicSize，并留 10% 余量。最小半高为 7.1。脚本只挂在 WorkspaceCamera，不修改 Transform、TopCamera 或纹理，不读取输入；可直接在 Inspector 调整余量与最小半高。

## 实施与验证结果

按上述范围完成，无新增包或外部源码采用。正式增加 SimulationWorkspaceCamera 和场景配置；复用现有官方 uGUI、字体及材质。当前主编辑器实际 Play 的 571×721、1024×768、1280×720 均显示完整棋盘、三色顶面/侧面和 1:1 固定预览。初次宽屏检查发现预览贴近说明文字，最终将预览上边界改为距 Canvas 顶部 112 单位，再重新查看三种尺寸截图。

相机独立性检查中只把操作相机绕棋盘旋转 35°，实验纹理与配置前、配置后各采样图像的 PNG SHA-256 全部一致；之后恢复姿态。每个状态只有一个启用的 AudioListener、无丢失脚本。原棋盘、积木、灯光、网格模板及实验 Camera 的序列化块保持不变。已恢复原 Game 尺寸与原 Play 状态；临时配置代码移出 Assets。详见 `.scratch/task-a-simulation/workspace-view/README.md` 与 `verification.json`，教学说明见 `docs/task-a-workspace-view-guide.md`。
