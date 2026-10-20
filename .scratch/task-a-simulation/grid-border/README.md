# 缩小预览底边修复验证

2026-10-16；当前主工程 Unity 6000.3.24f1，实际 Camera.Render、ReadPixels、GPU Graphics.Blit，非修改用户截图。

## 对照结果

- `before-512.png`：外框原宽度 0.04，原始图中线仍存在。
- `before-280-halfpixel.png`：原宽度缩小并偏移半个目标像素后，底边接近背景，复现用户现象。
- `after-512.png`、`after-280-halfpixel.png`：仅四条外框加粗到 0.08，内部格线保留 0.04。
- `game-1024x768.png`、`game-1280x720.png`：主编辑器真实 Game 输出。
- `before-checks.txt`、`after-checks.txt`：像素检查原始输出。验收只取 z=0、10 的外框行；其他内部线读数是诊断信息，不采用与外框相同的深色阈值要求。
- `verification.json`：32 项顶底边检查及三组各 114 项格位拾取结果；跨 Game 尺寸 RGB SHA256 相同。

## 回归命令与信号

在临时 Editor 桥接工具仍加载时运行：

```powershell
& '.\.scratch\task-a-simulation\diagnostics\TestGridBorder.ps1' -Name 'border-red'
```

修复前输出 `Rendered top/bottom borders: 18/32 passed; 14 failed.`，并以非零退出码结束。

外框加粗到 0.08 后，`border-trial008`、保存脚本与场景后重新 Play 的 `border-fixed008` 均输出 `32/32 passed; 0 failed.`。

采样尺寸为 512、384、280、256，目标像素偏移为 0、0.25、0.5、0.75。空白右侧区域的深灰线颜色为 RGB 64，要求边框邻域至少存在平均亮度不大于 75 的一行。偏移用于覆盖 UI 缩放后落在不同采样位置的情况，不声称重放了用户窗口的全部状态。

## 清理与限制

运行时只有 SimulationGridView 的外框宽度逻辑变化。临时 `SimulationPointerSetup.cs` 与 `.meta` 已移入 `../diagnostics/`；主工程编辑器程序集已重新编译并确认不再包含该工具。清理在 Edit 模式完成后恢复 Play，Game 尺寸设置已恢复。

桥接脚本目前是归档，不会自动运行；重新使用上面的检查前需临时加载归档工具并刷新 Unity。这些工具不是产品功能，也不进入构建。旧 RunGridPointerVerification.ps1 针对加粗前 RGB 哈希，不作为本次回归入口。

格位拾取的三组自动检查均通过，但此前鼠标悬停步骤仍有单独的人工交互验收项。本次不把这些拾取检查当作全部鼠标交互已验收的证据。

本次只保证记录的尺寸和采样条件；更小的预览仍受采样分辨率限制。积木遮住的棋盘边线不应强行画到积木上方。
