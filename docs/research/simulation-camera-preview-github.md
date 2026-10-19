# 仿真相机图像预览：GitHub 检索与有限借鉴

日期：2026-10-15。

## 范围与依据

依据用户本轮重申的任务 A：在现有原型中逐步学习搭建固定 10×10、三色积木、鼠标编辑、顶置相机、RGB 与 Ground Truth 的仿真平台。旧 `docs/requirements-v1.md` 为历史实体照片塔防需求，不把其中格纸、角色数量等旧约束套用于本轮。

当前小步：启用官方 uGUI，把已有 TopCameraRGB 实时显示到 Game 窗口，保持正方形比例。先验证显示链路；鼠标编辑和 GT 导出作为后续教学步骤。

## 检索与实际阅读

关键词：`site:github.com/Unity-Technologies uGUI RawImage license`、`site:github.com keijiro CameraCapture README license`。

| 候选 | 已读文件 | 授权与适配判断 |
| --- | --- | --- |
| [Unity-Technologies/uGUI](https://github.com/Unity-Technologies/uGUI)，main | com.unity.ugui/README.md、LICENSE.md、Runtime/UGUI/UI/Core/RawImage.cs；同时读取本机随 Unity 6000.3.24f1 附带的 2.0.0 package.json、LICENSE.md 与 RawImage 实现 | Unity Companion License。本机包依赖现有的 ui、imgui 内置模块，直接通过 Package Manager 启用 2.0.0；采用原版 RawImage、Canvas 与布局组件，不复制源码。 |
| [keijiro/FFmpegOut](https://github.com/keijiro/FFmpegOut)，master | README、LICENSE.md、Assets/FFmpegOut/Runtime/CameraCapture.cs，重点是 targetTexture、临时纹理与额外屏幕显示的分工 | 包源码 MIT，二进制包另有 GPL 授权；README 限定桌面平台。本轮不需要视频编码，整包依赖和平台范围不合适，不安装、不复制其代码。仅参考采集纹理与屏幕显示分别组织的思路。 |

维护与兼容性：GitHub main/master 用于机制比较，不声称其 HEAD 即当前 Unity 版本的实现。实际依赖为本机编辑器提供的 uGUI 2.0.0，后续用同版本编辑器验证；没有引入 Android 原生插件或联网运行需求，Android 构建尚未验证。

## 有限整合方案

1. 在 Packages/manifest.json 声明 com.unity.ugui 2.0.0，由 Unity 解析内置包并生成 lock 文件；不复制整个 GitHub 仓库。
2. 顶置相机继续写入既有 512×512 RenderTexture。显示用 Canvas 和 RawImage 引用同一资源，使用 FitInParent 的 1:1 布局。
3. 预览 UI 使用 Screen Space Overlay，与物理棋盘分开组织，不将 UI 内容烘入采集图。后续鼠标编辑需要由 UI 显示矩形转换到相机视口，不能直接沿用全屏坐标。
4. 本步不新增运行时控制脚本；通过 Unity 原有组件和序列化场景连接，保持 Simulation 模块的职责。
5. UI 包授权随安装包保留，直接依赖记录追加到 THIRD_PARTY_NOTICES.md。

## 验证计划

- 包解析成功、没有新增编译错误。
- 场景的 RawImage 引用与 TopCamera.targetTexture 为同一纹理。
- 在独立 Unity 检查目录中运行原场景，确认屏幕显示三色积木和完整棋盘，矩形窗口中预览保持正方形。
- 记录实际完成范围；不以已显示预览宣称整个任务 A 已验收。

## 实施与验证结果

- 已接入本机随编辑器附带的 uGUI 2.0.0，来源 BuiltIn；manifest 与 lock 版本一致。lock 的新增条目使用独立 Unity Package Manager 实际生成的元数据，其 ui/imgui 依赖已在主项目中存在。
- 新增场景对象 SimulationCanvas、PreviewBackground、PreviewArea、CameraPreview，使用原版 Canvas、CanvasScaler、GraphicRaycaster、Image、RawImage、AspectRatioFitter；没有新增运行时脚本。
- 独立 Unity 实际进入 Play，1365×1040 输出中方形预览的逻辑尺寸为 742.9298×742.9298；RawImage 与相机引用同一纹理；屏幕图像中三色积木均通过像素检查。已人工查看 captures/game-preview.png，棋盘完整、画面为正方形。
- 合入主场景时只追加 UI 对象与根引用。逐对象、组件核对，最终主场景与已验证的场景一致。
- 新建测试目录启动时出现一次 UnityEditor.Search.SearchDatabase 索引异常；堆栈未涉及项目代码，没有 C# 编译错误，随后 Play 与图像检查正常通过。该条保留在 camera-preview.log 中，不把它记为无异常日志。
- 本轮未实现鼠标编辑、真实状态模型或 RGB/GT 导出。用户当前打开的主编辑器需重新加载场景并运行后查看新 UI。

### 当前主编辑器的补充验证

用户反馈 Game 提示 No cameras rendering 后，读取正在运行的 Unity 进程内场景：缺少 SimulationCanvas，而磁盘场景已包含它。已在当前编辑器内接入 UI、保存并实际运行，得到 982×730 的 Game 截图，三色像素检查通过，UI 无丢失脚本。一次性诊断代码已移出 Assets；证据见 `.scratch/task-a-simulation/live-game-running.png` 与 `live-preview-verification.txt`。后续排查当前编辑器状态时，不能只以磁盘 YAML 或独立测试工程代替实际内存状态。
