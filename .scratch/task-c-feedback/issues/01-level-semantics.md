# 01 关卡语义映射（Unity-free）

Status: resolved

## 目标

在 `Assets/Scripts/Application` 增加颜色→角色的语义映射与关卡状态，供 C 的两个 Unity 组件共用，并可被纯 C# 检查覆盖。

## 验收

- `LevelRuleSet` 显式配置颜色→`GameRole`（Core/Wall/Tower），未配置颜色必须报出问题而不是静默忽略。
- `LevelState` 从 `BoardSnapshot` 构建，保存占用、颜色、角色与来源版本；不修改快照。
- 合法性：恰好一个核心；无核心、多核心、存在未映射颜色都进入 `Issues` 且 `IsPlayable=false`。
- 检查覆盖：正常布局、空棋盘、未映射颜色、双核心、`RoleAt` 越界、版本与占用一致性。

## Answer

已实现 `Assets/Scripts/Application/LevelSemantics.cs`：`GameRole`、`LevelRuleSet`（显式颜色规则，拒绝空/重复/None 规则）、`LevelCell`、`LevelState`（占用、颜色、角色、来源版本、Issues、IsPlayable、Describe）。合法性覆盖缺核心、多核心、未映射颜色；越界访问抛异常；快照与规则为空时拒绝。检查 `Tests/LevelSemanticsChecks.cs` 22 项通过。