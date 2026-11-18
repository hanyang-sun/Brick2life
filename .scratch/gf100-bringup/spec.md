Status: needs-triage
Type: task

# GF100 首阶段落地（连续识别）

依据需求：`output/Brick2Life_实机硬件需求与验收说明.docx`（2026-11-10 17:32 版）
上游文档：`output/GF100_落地注意事项.md`、`output/Brick2Life_推荐硬件清单_BOM.md`

## 目标

第一阶段把 USB 相机实时画面接入 Unity 识别链路，在**投影全程常亮**的条件下验证格位、颜色、占用状态能随积木摆放/移动/移除更新。单层红绿积木，可重复测试投影画面。

## 已定硬件

| 项 | 选择 | 说明 |
|---|---|---|
| 相机 | GF100（1280×720，卷帘快门） | 便宜、参数表完整；先跑通流程，必要时换全局快门 |
| 投影仪 | 红米投影仪 3 Lite（投射比 1.2:1） | 600 mm 处 500×281 mm，满足短边 ≥270 mm |
| 镜头 | GF100 原配（焦距待确认） | 需核对是否 2.5 mm / 能否换镜 |

## 判据来源（不是猜的，来自代码与文档）

`Assets/Scripts/Vision/PhotometricProfile.cs` 的模型：

```
camera(ch) = reflectance(ch) × ( ambient(ch) + gain(ch) × projectorCommand(ch) ) + bias(ch)
```

由此推出四条硬约束：

| 代码位置 | 约束 | 违反后果 |
|---|---|---|
| L38、L57 `exposureLocked` | 曝光/增益/白平衡必须锁死 | 构造函数抛异常，整链不可用 |
| L63–L77 `ambient`/`gain` 每通道单标量 | 图像必须**空间均匀**（无条纹、无渐晕、投影均匀） | 反解算错颜色 |
| L11 `ClipLevel = 0.985`；L109 `TryRecover` | 不能过曝 | 该像素被拒，识别出空洞 |
| L71 `signal <= 1e-4` 抛异常 | 投影黑时环境光要够 | 标定 `Fit()` 失败 |

需求文档 04 节第 4 步另加一条：**相机帧必须能与对应投影内容匹配**。这是卷帘快门被谨慎对待的根因。

## 问题总表（按硬件逐项）

### ① 俯拍相机（GF100）

| # | 问题 | 类型 | 状态 | 文件 |
|---|---|---|---|---|
| 01 | 实际焦距与水平视场角未确认（2.5 mm 配 1/4" 只有水平 75°，做不到标称 100°） | research | needs-info | [01](issues/01-confirm-lens-focal-and-fov.md) |
| 02 | 曝光/增益/白平衡能否手动锁定 —— **机制已证实**（AMCap 截图有手动滑杆 + 自动勾选框），待在设备上确认锁住 | research | ready-for-human | [02](issues/02-confirm-manual-exposure-lock.md) |
| 03 | USB 线为非标 4P-2.0 mm，默认仅 1.5 m | task | ready-for-human | [03](issues/03-specify-cable-length-at-order.md) |
| 05 | Unity 实际拿到的像素格式与帧率未知（YUY2 只有 5 fps） | task | ready-for-human | [05](issues/05-verify-unity-capture-format-and-fps.md) |
| 06 | 投影横纹与卷帘帧内错位未实测 | task | ready-for-human | [06](issues/06-measure-flicker-and-intra-frame-mismatch.md) |
| 11 | MJPEG 4:2:0 色度子采样对红绿分类的影响未评估 | research | needs-triage | [11](issues/11-assess-chroma-subsampling.md) |
| 12 | 出厂默认 清晰度 80 / 伽玛 300 属非中性值，需改为基线并写入启动流程 | task | ready-for-agent | [12](issues/12-establish-camera-parameter-baseline.md) |

### ② 成品投影仪（红米 3 Lite）

| # | 问题 | 类型 | 状态 | 文件 |
|---|---|---|---|---|
| 04 | 能否关闭 MEMC / 动态亮度 / 自动避障未证 | research | needs-info | [04](issues/04-confirm-projector-auto-features-off.md) |
| 13 | **采购前置门槛**：投射比合格 ≠ 能近焦。标称最小画幅 40" 折算最短距离 1.063 m，而工况只要 0.60 m（56%） | task | ready-for-human | [13](issues/13-measure-projected-frame-geometry.md) |
| 14 | 朝下安装姿态、连续运行散热、Windows 独立扩展显示器识别 | task | ready-for-human | [14](issues/14-projector-downward-mount-and-extended-display.md) |
| 15 | 过曝与画面均匀性（"最亮画面须保留积木顶面颜色细节"） | task | ready-for-human | [15](issues/15-projector-overexposure-and-uniformity.md) |

### ③ 棋盘、积木与标定件

| # | 问题 | 类型 | 状态 | 文件 |
|---|---|---|---|---|
| 16 | 底板：哑光不足会亮斑、印刷可能非 100% 比例、格线可能进采样区 | task | ready-for-human | [16](issues/16-base-board-material-and-scale.md) |
| 17 | 积木红绿可分性与阈值余量（`MaximumColorDistance` 0.35 / `MinimumColorMargin` 0.12） | task | ready-for-human | [17](issues/17-bricks-and-red-green-separability.md) |
| 18 | AprilTag 定位标记：数量、尺寸、位置、相机可见性、是否被投影覆盖 | task | ready-for-human | [18](issues/18-apriltag-markers.md) |
| 20 | 灰卡与桌面反射率 `ρ_table`（`Fit()` 的基准输入，错了所有颜色整体偏移） | task | ready-for-human | [20](issues/20-gray-card-and-table-reflectance.md) |

### ④ 支架与照明

| # | 问题 | 类型 | 状态 | 文件 |
|---|---|---|---|---|
| 19 | 支架：偏移量（**原"近同轴 ≤30mm"已作废，改见 24**）、承重与偏心力矩、防坠固定 | task | ready-for-human | [19](issues/19-mount-near-coaxial-and-load.md) |
| 21 | 照明与遮光：色温固定、均匀漫射、条纹/亮斑/阴影检查（投影黑时 `ambient` 靠它撑住） | task | ready-for-human | [21](issues/21-lighting-and-shading.md) |
| **24** | **相机自遮挡**：36 mm 机身挡投影 → 板面 72 mm 暗斑（2.9 格）；比积木阴影严重得多，最优偏移 d≈100 mm | task | ready-for-human | [24](issues/24-camera-self-shadow-vs-coaxial.md) |

### ⑤ 电脑与线材

| # | 问题 | 类型 | 状态 | 文件 |
|---|---|---|---|---|
| 22 | USB 数据口与 HDMI 第二屏；USB-C 转接须先验证 DP Alt Mode 与扩展显示 | task | ready-for-human | [22](issues/22-computer-ports-and-second-display.md) |
| 23 | HDMI 线端口匹配、EDID 丢失、应力释放 | task | ready-for-human | [23](issues/23-hdmi-cable-edid-and-strain-relief.md) |

### ⑥ 识别与确认代码（跨硬件，但由上述实测结果驱动）

| # | 问题 | 类型 | 状态 | 文件 |
|---|---|---|---|---|
| 07 | `FrameObservation` 无投影签名，streak 可跨投影状态累计 | task | ready-for-agent | [07](issues/07-bind-streak-to-projection-signature.md) |
| 08 | `StableDuringExposure` 仍是仿真常量而非时序判定 | task | ready-for-agent | [08](issues/08-compute-stable-during-exposure.md) |
| 09 | 拒绝原因未区分"内容变了"与"卷帘下无法保证" | task | ready-for-agent | [09](issues/09-split-rejection-reasons.md) |
| 10 | 投影内容更新率与相机帧率未做互质/节流 | task | ready-for-agent | [10](issues/10-throttle-projector-content-and-decouple-fps.md) |

### 统计

| 状态 | 数量 | 编号 |
|---|---|---|
| needs-info | 2 | 01、04 |
| needs-triage | 1 | 11 |
| ready-for-human | 14 | 02、03、05、06、13–23 |
| ready-for-agent | 5 | 07、08、09、10、12 |

## 关键依赖关系

```
02 ──不成立则整个方案作废（exposureLocked 为 false 时任何识别都不成立）
06 ──→ 决定 08 的 RequiredStableSpan 取 T_exp 还是 T_readout + T_row
08 ──→ 09、10 才能验证效果
07 ──与快门无关，任何时候都该先做
12 ──依赖 02（先能锁，才谈基线）
15 ──依赖 13（先知道幅面，才谈均匀性）
17 ──依赖 12、20（先能锁参数、先有 ρ_table，才谈颜色可分）
19 ──依赖 13、14（先知道投影姿态与幅面，才定支架）
23 ──依赖 13、22（先确认分辨率与端口，才定线）
```

## 建议动手顺序

1. **02**（问卖家 / 实机确认锁定）—— 5 分钟的事，但决定成败
2. **01、04**（问卖家）—— 同时发出去
3. **07**（纯代码，无外部依赖）
4. **03、23**（下单时顺手指定线长与端口类型）
5. **05、16、21**（到货即可做，不依赖投影）
6. **13、14、15、19**（投影到位后）
7. **06**（决定 08/10 要不要真做 —— 若实测干净，08 与 10 可大幅简化甚至不做）
8. **17、20**（最后，因为依赖参数锁定与照明固定）

## Comments

用户在 2026-11-10 决定第一阶段先用 GF100 落地，并追问需要注意事项；本 spec 把该轮分析转成可追踪任务。
随后用户指出「要的是所有硬件的相关问题」，遂按硬件逐项补齐 13–23（投影、底板、积木、标记、支架、灰卡、照明、电脑、HDMI 线），并把总表重组为按硬件归类。
再后用户提出「红米 3 Lite 只有通过近焦测试才值得买」——核对成立：标称推荐画幅 40–100 英寸，40" 折算最短投射距离 1.063 m，而工况要 0.60 m（仅 56%）。据此把 issue 13 升级为**采购前置门槛**，BOM 中红米 3 Lite 降为"条件推荐"，并补两条替代路线（坚果 P3S 官方 0.5–3 m 覆盖；或改光学布局放远投大画面、只用中央区域）。
