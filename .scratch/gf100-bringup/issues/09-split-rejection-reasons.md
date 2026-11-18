Status: ready-for-agent
Type: task
Blocked by: 08

# 09 拒绝原因细分：区分"内容变了"与"卷帘下无法保证"

规格见 [spec](../spec.md)。

## 问题

`ProjectedLightRecognizer.Reject(...)` 目前用一个笼统理由覆盖所有失败帧，例如 L34 的
`"曝光期间投影内容发生了变化。"`

需求文档 05 节要求验收记录包含「过曝、待确认格」，并「记录端到端延迟、过曝或待确认格，作为后续调参依据」。若所有失败都混成一句"投影内容变了"，就**无法区分**：

| 实际情形 | 说明 | 应对 |
|---|---|---|
| 投影内容真的在曝光期内切换了 | 内容更新率太高 | 降更新率（issue 10） |
| 卷帘下无法保证整帧同一内容 | 快门机制问题，与内容更新无关 | 换全局快门，或接受打折的帧可用率 |
| 过曝 | 曝光/投影亮度设置问题 | 调曝光策略 |
| 遮挡 | 手/物体挡住 | 正常，等待后续帧 |
| 匹配不到投影内容 | 帧-内容配准失败 | 查时间戳与延迟 |

前三项的处理手段完全不同，合并统计会导致调参时走错方向。

## 待做

1. 把拒绝原因改为**可枚举的分类**（而不是自由字符串），例如：
   `ExposureNotLocked / ProjectionChangedDuringExposure / RollingShutterUncertain / Overexposed / Occluded / ContentUnmatched / BoardNotInView / SamplingMismatch`。
2. 在运行统计里按分类计数，并输出到验收记录。
3. 特别要能区分 `ProjectionChangedDuringExposure`（真变了）与 `RollingShutterUncertain`（快门限制）——前者归因于内容策略，后者归因于硬件。

## 验收

- 跑一段固定长度的连续识别，输出各分类的计数表；
- 在"投影内容 60 Hz 快变"与"内容静止"两种工况下，计数分布应有明显区别；
- 该表可直接贴进需求文档 05 节的验收记录。

## Answer

（待填：枚举定义、统计输出位置、两种工况的计数对比）
