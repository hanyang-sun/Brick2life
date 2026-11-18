Status: ready-for-agent
Type: task
Blocked by: 06

# 08 StableDuringExposure 由时序计算，而非仿真常量

规格见 [spec](../spec.md)。

## 问题

`Assets/Scripts/Vision/CellProjection.cs:34-37`：

```csharp
// Continuous play: the projector shows the game frame for the whole exposure and never blanks.
public static CellProjection Continuous(double[] red, double[] green, double[] blue)
{
    return new CellProjection(red, green, blue, true, true, 0);
}
```

第三个 `true` 就是 `StableDuringExposure`——**在仿真里被直接假定为真**。而 `ProjectedLightRecognizer.cs:33-34` 用它当闸门：

```csharp
if (!projection.StableDuringExposure)
    return Reject(..., "曝光期间投影内容发生了变化。");
```

实机上"曝光期间投影内容有没有变"**必须算出来**，不能假定。

## 判据的物理换算

- **全局快门**：曝光窗口 = `[t0, t0 + T_exp]`，单一区间。
- **卷帘快门**：第 k 行窗口 = `[t0 + k·Δt, t0 + k·Δt + T_row]`，整体跨度 = `T_readout + T_row`。

所以"曝光期间投影无更新"的窗口：

```
RequiredStableSpan = 全局 → T_exp
                     卷帘 → T_exp + T_readout     （保守取整帧周期）
```

720p60 下卷帘的跨度约 16.7 ms，与投影帧周期相等——这正是 GF100 的风险点。

## 待做

1. `CellProjection` 增加：`ShutterKind { Global, Rolling }`、`ReadoutDuration`、`ExposureDuration`；
   `StableDuringExposure` 改为**由构造或工厂方法算出**，不再由调用方随意传 true。
2. 引入投影内容时间线（例如 `ProjectorCommandTimeline`：记录每次 content 切换的 timestamp + command），
   由 `TryComputeStable(t0, span)` 判定是否有切换落在窗口内。
3. `Continuous()` 保留给仿真，但**明确标注为仿真假设**，实机路径不得使用。
4. 回归：模拟"内容在曝光窗口内切换一次"，断言 `StableDuringExposure == false` 且该帧被 `Reject`。

## 取值依赖 issue 06

- 06 实测**无帧内错位** → 卷帘也可取 `T_exp`，实现更简单；
- 06 实测**有帧内错位** → 必须取 `T_readout + T_row`。

建议先按后者实现（保守），实测后若证明可放宽再调。

## Answer

（待填：接口改动、判定实现、回归结果、最终取值依据）
