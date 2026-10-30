# 04 Unity 视图、投影与分层

Status: resolved

## 目标

在 `Assets/Scripts/Simulation` 增加 `SimulationLevelAdapter`、`SimulationLevelView`、`ProjectionSimulator`，并由 Editor 菜单完成场景装配与分层。

## 验收

- `SimulationLevelAdapter.Apply(BoardUpdate)`：调用 `LevelFeedbackSession`，刷新关卡视图与投影，更新状态文字；非法关卡保留上一版游戏对象并提示原因。
- `SimulationLevelView`：按角色生成可区分的对象（核心/墙/塔形状与高度不同），位置为格中心，整体位于 `Brick2LifeGame` 层；差异更新（同角色复用、消失的销毁）。
- `ProjectionSimulator`：按 `ProjectionPlan` 生成/复用面片，贴在 `brickTop`/`table` 高度 + 偏移，`shadowCastingMode` 关闭，位于 `Brick2LifeProjection` 层；提供开/关与强度，用于投影对照。
- 分层：`TagManager` 增加 30/31 层名；`TopCamera` 掩码排除 30；操作相机保持“全部除 UI”。
- Editor 菜单 `Brick2Life/Simulation/Install feedback`：创建根对象、材质资产、组件与接线、相机掩码，并给出可核对日志。
- 人工目标演示：`SimulationFeedbackDemo` 按固定样例布局提交观测，界面注明“人工目标（非识别结果）”，可禁用。

## 说明

本机无 Unity 6.3，以上只在 Unity 2022.3 托管程序集上做编译基线验证；运行时行为写在手册中待用户在实际编辑器中确认。

## Answer

已实现 `Brick2Life.Feedback` 程序集：`SimulationFeedbackBootstrap`（执行顺序 -50，建立 `BoardSyncSession`）、`SimulationLevelAdapter`（`Apply(BoardUpdate)`、角色→颜色反查、状态文字）、`SimulationLevelView`（按角色生成/复用/销毁对象、格中心定位、层 30、关闭阴影与 Collider）、`ProjectionSimulator`（面片池、表面高度、`SetVisible`/`SetIntensity`、`MeasureAlignmentError`）、`SimulationFeedbackDemo`（F5/F6/F7 人工目标，明确标注非识别结果）。`Assets/Editor/SimulationFeedbackSetup.cs` 提供安装菜单与分层复核菜单；`TagManager` 已写入 30/31 层名，场景顶置相机掩码已排除层 30。

未在 Unity 中运行（本机无 6.3），已在 `docs/task-c-feedback-guide.md` 第 6 节写明核对步骤；编译基线通过。