# 02 投影计划（Unity-free）

Status: resolved

## 目标

把“要投什么、投在哪一格、投在哪个表面”表达成与引擎无关的数据，使投影内容可检查、可复现，Unity 侧只负责把标记画出来。

## 验收

- `ProjectionMark`：格位矩形（`X/Y` 左上角 + `SizeX/SizeY`，单位=格）、种类（格点/角色高亮/变更提示）、颜色（RGBA 强度）、表面（桌面/积木顶面）。
- `ProjectionPlanBuilder`：格点为 11×11 交点（尺寸固定、只投桌面）；角色高亮只覆盖占用格并按其角色取色；变更提示只覆盖 `BoardDelta` 涉及格，新增/移除/改色使用不同色调。
- 计划按固定顺序输出（行优先、同格按种类），可复现；标记数量有上限并报出截断。
- 空棋盘只产生格点；非法的未映射颜色格不产生角色高亮。
- 检查覆盖：各类标记数量、表面选择、颜色差异、上限截断、顺序稳定。

## Answer

已实现 `Assets/Scripts/Application/ProjectionPlan.cs`：`ProjectionMark`（float 格位矩形，支持交点与格内矩形）、`ProjectionSurface`、`ProjectionChange`、`ProjectionColor`、`ProjectionOptions`、`ProjectionPlanBuilder`。格点 11×11 并按相邻占用选择表面；角色高亮只覆盖有角色的占用格；变更提示来自 `BoardDelta` 且拒绝版本不匹配的差异；`MaxMarks` 截断；输出顺序固定。检查 `Tests/ProjectionPlanChecks.cs` 25 项通过。