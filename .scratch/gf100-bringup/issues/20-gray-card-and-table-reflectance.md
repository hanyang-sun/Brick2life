Status: ready-for-human
Type: task
Blocked by: 16

# 20 灰卡与桌面反射率标定

规格见 [spec](../spec.md)。

## 问题

`PhotometricProfile.Fit` 需要一个**已知反射率**作为基准：

```csharp
public static PhotometricProfile Fit(string calibrationId, RgbColor darkFrame, RgbColor tableUnderBlack,
    RgbColor tableUnderWhite, RgbColor knownTableReflectance, IReadOnlyList<ColorSwatch> swatchesUnderBlack,
    bool exposureLocked)
```

内部按通道解方程（L63-77）：

```
ambient = (tableBlack − bias) / ρ_table
gain    = (tableWhite − tableBlack) / ρ_table
```

其中 `ρ_table` 就是 `knownTableReflectance`——**桌面（PVC 板）的反射率**。若这个值不准，`ambient` 与 `gain` 会**同比例错**，而 `TryRecover` 又靠 `ambient`/`gain` 反解 reflectance，于是**所有颜色都会整体偏移**。

代码还带两条硬检查：

- `if (rho < 0.02) throw("灰卡反射率过低，无法分开环境光和投影增益。")`（L68）
- `ambient`/`gain` 必须落在 (0, 4]（L75-76）

## 待做

1. 用 **18% 中性灰卡**在**实际工作照明**下确定曝光，使灰卡读数落在中灰附近；
2. 在同一曝光下拍**白色 PVC 板**，由「灰卡已知反射率 → 板面读数」反算 `ρ_table`；
   - 注意：需要做**线性化**（用 `profile.Decode` 的同一 gamma），不能在 sRGB 直读值上直接比例换算；
3. 记录：`ρ_table` 三通道值（红/绿/蓝可能略有差异，PVC 通常偏中性）、照明色温、曝光/增益/白平衡值；
4. 复核 `ρ_table` 各通道 ≥0.02（否则 `Fit()` 抛异常）；
5. 之后若更换底板、照明或投影亮度，**`ρ_table` 必须重测**。

## 容易漏的一点

`Fit()` 还要 `swatchesUnderBlack`——**每种积木颜色在"投影全黑"下的一张照片**。所以标定流程实际需要：

| 输入 | 怎么拍 |
|---|---|
| `darkFrame` | 盖住镜头（或关灯）拍，得到 `bias` |
| `tableUnderBlack` | 投影全黑，拍空板 |
| `tableUnderWhite` | 投影全白，拍空板 |
| `knownTableReflectance` | 由灰卡反算 |
| `swatchesUnderBlack` | 投影全黑，分别拍红、绿积木 |

这五组拍摄**必须在同一曝光/增益/白平衡下完成**，中途任何一项变动都会让标定作废。

## Answer

（待填：ρ_table 三通道、照明色温、曝光/增益/白平衡、五组拍摄的完整记录）
