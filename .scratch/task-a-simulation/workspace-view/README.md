# 当前主编辑器运行验证：三维工作台与顶置小预览

日期：2026-10-16，Asia/Shanghai。正式工程：`D:/桌面/计算机图形学/BrickDefenseMVP`；场景：`Assets/Scenes/Brick2LifeSimulation.unity`；Unity 6000.3.24f1。

## 实际执行

本轮在原来已打开的主编辑器中操作，没有启动第二个实例打开主工程。配置前实际内存记录见 `loaded-state.txt` 的最初状态已被后续重载覆盖；配置前的独立留存为 `before-state.txt`、`edit-before-state.txt` 和 `before-apply-state.txt`。当时用户正在 Play，先保留截图和 RGB，再停止运行；磁盘场景备份见 `scene-before.unity`。

配置完成后在同一个编辑器实际进入 Play，检查当前窄 Game 区域以及 1024×768、1280×720 两个固定尺寸。尺寸测试只临时修改内存中的 GameView 尺寸，最后恢复原尺寸设置及原来的 Play 状态。

## 已通过

| 实际截图 | 主画面三色像素数 R/G/B | 小预览三色像素数 R/G/B | 结果 |
| --- | --- | --- | --- |
| [571×721 当前窗口](workspace-current.png) | 1139 / 1124 / 1124 | 156 / 144 / 144 | 棋盘完整，顶面/侧面可见，预览正方形 |
| [1024×768](workspace-1024x768.png) | 3641 / 3615 / 3640 | 529 / 529 / 529 | 棋盘完整，标题与预览无重叠 |
| [1280×720](workspace-1280x720.png) | 4177 / 4177 / 4179 | 650 / 650 / 650 | 棋盘完整，标题与预览无重叠 |

以上最终截图均已实际查看。宽屏初次截图曾出现预览靠近说明文字，最终改为顶部固定留白 112 Canvas 单位，并重新拍摄上表三张图。

- 各状态快照中仅有一个启用的 AudioListener，位于 WorkspaceCamera；所有对象缺失脚本数为 0。能够实际进入 Play，运行中的取景尺寸随主视口宽高比调整。
- WorkspaceCamera 为 MainCamera，正交、直接输出屏幕；TopCamera 为 Untagged，仍是位置 (5,12,5)、旋转 (90,0,0)、Size=5.5、TopCameraRGB。
- 各状态文件中的棋盘四角均位于操作相机视口的 0～1 内；运行脚本额外按 Y=0～1 的八角点计算单层取景。CameraPreview 的运行矩形宽高相等。
- 配置前与配置后、窗口尺寸改变前后、操作相机绕棋盘旋转 35°前后的实验图像 PNG 字节完全一致，SHA-256 均为 `C549683E5FBB1F4559BB0EF44FF617ADE4E78FF9791D6828860B4AC6A2B55C40`。实际旋转截图为 `workspace-rotated.png`，对应实验图像为 `workspace-rotated-rgb.png`。该截图来自最后一次 UI 顶部留白微调之前，只用于相机独立性验证。
- 场景按原有 fileID 比较：只改动 SimulationRoot 子列表、Canvas 子列表、两个预览区域的 RectTransform、TopCamera 标签及监听组件；新增 WorkspaceCamera 与三个文字对象共 17 个序列化块。原棋盘、三块积木、网格线模板、灯光和实验 Camera 的序列化块保持一致。明细见 `scene-changes.json`。

像素与监听检查结果见 `verification.json`。这些颜色计数只验证图像显示存在三色，不构成任务 B 的识别准确率评价。

## 交付与限制

正式运行脚本只有本步新增的 `Assets/Scripts/Simulation/SimulationWorkspaceCamera.cs` 和原有网格脚本。临时 Editor 配置代码完成后归档到 `.scratch/task-a-simulation/diagnostics/SimulationWorkspaceSetup.cs`，不随场景运行，也不重新启用历史自动修复工具。

本步完成固定三维主视图、侧边实验预览与取景适配。尚未加入鼠标旋转/缩放、积木编辑、GT 导出、识别或投影。当前取景范围只覆盖单层 Y=0～1；极窄窗口内预览及棋盘会变小，后续工具界面仍需单独处理布局。

清理临时工具时，首次在 Play 中触发脚本热重载，日志出现三条 `The referenced script (Unknown) on this Behaviour is missing!`。随后重新从当前进程读取实际场景，所有对象仍为 missing=0，见 `post-hot-reload-state.txt`。因此改为停止 Play 后移出工具，并核对 `cleanup-edit-state.txt`：playing=False、所有对象 missing=0。最终清理编译日志为 Tundra build success，未再出现上述缺失脚本提示；之后用 Unity 的 Play 快捷键恢复运行，新的启动日志也未出现缺失脚本或 C# 错误。日志仍有 `Native extension for WindowsStandalone target not found`，不据此宣称构建模块已验收或日志完全无警告。

窗口级诊断截图不作为 Game 验收依据；图像验收仅采用上面通过 Unity ScreenCapture 得到的三张实际 Game 截图。

新增对象、参数、脚本变量、Awake/LateUpdate 执行过程及学习方法见 `docs/task-a-workspace-view-guide.md`。不使用历史日志中的错误推断本轮编译失败，也不宣称项目历史日志完全无异常。
