Status: ready-for-human

# 修复缩小预览中底部横线变淡

用户 2026-10-16 截图 QQ20261016-185427.png。原始 512×512 RGB 中边框存在；缩小预览的底边接近背景色。

## 验收

- 保留 X/Z=0～10 和内部格线 0.04，只调整外框显示。
- 真实 Unity Camera.Render 与 Graphics.Blit 输出，覆盖 512、384、280、256 像素及四种亚像素位置；顶底边均保留明显深色行。
- 保存当前主工程场景，重新进入 Play 后仍生效。
- 记录原因、文件作用和实际验证结果；不把截图修改作为修复。

## Comments

- 复用已研究的 LineRenderer 机制，本次属于现有网格渲染问题修复，无新增包或外部代码。
- 回归命令：.scratch/task-a-simulation/diagnostics/TestGridBorder.ps1。检查实际纹理中的空白区域，避免三色积木遮挡底边造成误判。
- 已实施：SimulationGridView 新增 borderWidth=0.08，场景保存同值；内部线仍为 0.04。
- 当前主编辑器真实渲染：修复前顶底边 18/32 通过，试验及重新 Play 后 32/32 通过。553×721、1024×768、1280×720 的拾取检查各 114 项通过，修复后 RGB 跨 Game 尺寸一致。
- 临时 Editor 工具和 .meta 已在 Edit 模式移入 diagnostics，重新编译确认程序集不再包含该工具，然后恢复 Play；查看清理时刻日志尾部未发现 C# 编译错误或 missing-script 消息。
- 实施完成，等待用户目视确认。证据与范围见 ../grid-border/README.md；不依赖用户确认才能完成代码修复。
