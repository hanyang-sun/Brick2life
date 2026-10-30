# 03 反馈会话：只消费预测事件

Status: resolved

## 目标

`LevelFeedbackSession` 把 `BoardUpdate` 转成“关卡状态 + 投影计划 + 变更说明”，并保证 C 不读取关卡真值。

## 验收

- 输入仅 `BoardUpdate`；`Baseline` 建立基线（无变更提示），`Changed` 重建并带变更提示，`Unchanged` 不推进版本、不产生变更提示。
- `Rejected`/`Stale` 保留上一次已确认状态，回报原因，不改变关卡与投影。
- 返回帧包含 `Changed` 标志，便于视图跳过无意义刷新；连续提交同一快照保持幂等。
- 检查覆盖：五类结果的版本、计划差异、重复提交、拒绝后仍可继续、只经 `BoardSyncSession` 产生输入（不构造 GT 快照）。

## Answer

已实现 `Assets/Scripts/Application/LevelFeedbackSession.cs` 与 `FeedbackFrame`：只接收 `BoardUpdate`，Baseline/Changed 重建并置 `Changed=true`，Unchanged 返回原对象且不推进版本，Rejected/Stale 保留状态并回传原因；拒绝跨棋盘定义、版本回退与差异基线不匹配的输入。检查 `Tests/LevelFeedbackChecks.cs` 26 项通过（输入全部由真实 `BoardSyncSession` 产生）。