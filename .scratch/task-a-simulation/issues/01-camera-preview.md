Status: ready-for-human

# 顶置相机的 Game 窗口预览

启用官方内置 uGUI 2.0.0，连接既有 RenderTexture；使用 1:1 自适应 UI 显示。完成包解析、场景引用与实际渲染验证。

## Comments

- 用户已授权安装需要的包；保留逐步学习方式。
- GitHub 比较见 docs/research/simulation-camera-preview-github.md。

## 实施结果

- 已完成包声明、内置包解析元数据与主场景 UI 接入，等待用户在当前编辑器中检查并学习组件。
- SimulationCanvas 使用 Screen Space Overlay；CameraPreview 的 RawImage 直接引用 TopCameraRGB，AspectRatioFitter 为 FitInParent、1:1。
- 没有新增运行时脚本；物理棋盘、积木、灯光、相机等原有对象逐项保留。
- 独立 Unity 检查：1365×1040 Game 输出，预览逻辑尺寸 742.9298×742.9298，红/绿/蓝像素计数 8280/8280/8368，纹理引用一致。
- 主场景与独立检查所用场景全部对象及组件比对一致；主编辑器仍需重新加载场景后运行。

## 当前主编辑器实测修复

- 用户报告 No cameras rendering 后，从当前 Unity 进程内导出真实状态：工程与场景路径正确，但 Hierarchy 没有 SimulationCanvas，TopCamera 的目标为 TopCameraRGB。磁盘文件存在 UI 不代表当前编辑器内存已加载。
- 通过一次性 Editor 诊断代码，在当前场景直接创建已验证的标准 uGUI 层级并保存，保留当前已有物理场景对象。随后直接在主编辑器进入 Play 验证。
- 结果 PASS：Game 982×730，红/绿/蓝像素 4034/4094/4157；Canvas、RawImage、AspectRatioFitter 均存在，纹理为 TopCameraRGB，丢失脚本数为 0。
- 实际画面见 live-game-running.png，检测结果见 live-preview-verification.txt；一次性诊断代码已移出 Assets，归档到 diagnostics/。
