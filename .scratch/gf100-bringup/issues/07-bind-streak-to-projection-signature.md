Status: ready-for-agent
Type: task

# 07 FrameObservation 携带投影签名，streak 绑定投影状态

规格见 [spec](../spec.md)。

## 问题

`Assets/Scripts/Vision/LayoutChangeTracker.cs:109`：

```csharp
if (pending != null && Same(pending, current)) streak++;
else streak = 1;
```

`Same()` 只比较 `BoardCell[]` 的格子状态（L131-136）。而 `Assets/Scripts/Core/Contracts/FrameObservation.cs` **没有任何投影 command 字段**——它只有 `Definition / FrameId / SessionId / Sequence / CapturedAtUtc / CalibrationId / Accepted / RejectionReason / Cells`。

所以确认逻辑无法证明"连续 3 帧是在同一投影状态下反解出来的"，streak 可能跨越一次投影内容更新。

## 为什么这是真缺陷（不只是理论）

- 平时：不同 command 通常导致格色不同，自然打断 streak，掩盖了问题；
- **卷帘的系统性错位**：每帧都是"上半 A、下半 B"、错法固定 → 3 帧**真的会互相一致** → 规则放行 → 提交错误布局。

也就是说，这个缺陷恰好会在最需要"3 帧一致"兜底的时候失效。

## 待做

1. `FrameObservation` 增加投影状态标识，例如
   `string ProjectionSignature`（内容哈希或 `(commandR, commandG, commandB)` 的稳定摘要），由 `ProjectedLightRecognizer.Recognize` 从 `CellProjection` 填入。
2. `LayoutChangeTracker` 持有 `pendingSignature`，改为：

```csharp
if (pending != null && signature == pendingSignature && Same(pending, current)) streak++;
else { streak = 1; pendingSignature = signature; }
```

3. 补回归：构造"格子状态相同但投影签名不同"的连续 FrameObservation，断言 streak 不被累加、不产生确认。

## 与快门的关系

**与卷帘/全局无关，都必须做。** 全局快门下这是防御性校验；卷帘下它是正确性的必要条件。

## Answer

（待填：改动文件与回归结果）
