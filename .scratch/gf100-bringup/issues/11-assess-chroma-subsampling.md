Status: needs-triage
Type: research

# 11 MJPEG 4:2:0 色度子采样对红绿分类的影响

规格见 [spec](../spec.md)。

## 问题

GF100 在 720p 要跑到 60 fps **必须用 MJPEG**（YUY2 只有 5 fps，见厂商帧率表，也见 issue 05）。而 MJPEG 通常伴随两个有损环节：

1. **8×8 DCT 块量化** → 块边界产生振铃/块效应，高对比边缘最明显；
2. **色度子采样**（常见 4:2:0）→ 色度平面宽高各减半，**色度分辨率只有亮度的一半**。

对 `ProjectedLightRecognizer` 的影响：

- 分类走的是 `TryRecover` → `Encode(reflectance)` → `ClassifyPixel`，其中颜色判别用**归一化 RGB 的欧氏距离**（L130-141，含 `MaximumColorDistance = 0.35`、`MinimumColorMargin = 0.12`）。
- 色度被 4:2:0 压缩后，**红/绿的色度差会被平滑到相邻像素**；在 25 mm 格子边缘与格线附近，采样点的色度可能"串"到隔壁。
- 若采样点落在格中央、且格内颜色均匀，影响应该很小；但**积木顶面只有 19–20 mm**，在 69 px/格（2.5 mm 镜头）下顶面只占约 55–75 px——子采样后的色度块是 2×2 像素，边界占比不算小。

## 待评估的问题

1. 该 UVC 模组的 MJPEG 是否真的用 4:2:0？还是 4:2:2 / 4:4:4？（需查 datasheet 或实测：抓一帧 MJPEG 码流解出 YUV 后看采样率）
2. 在 69 px/格与 100+ px/格两种情况下，`MinimumColorMargin = 0.12` 与 `MaximumColorDistance = 0.35` 这两个阈值是否仍有余量？
3. 是否需要把采样区从"整格内部"收紧到"格中央 N×N 像素"，以避开色度串扰？

## 建议的验证方法

- 用 `MinimumColorMargin` / `MaximumColorDistance` 的当前值，对**同一场景**分别用 MJPEG 与 YUY2（低帧率但无损色度）各抓 20 帧，比较红/绿块反解出的 reflectance 分布是否显著偏移；
- 若偏移明显，考虑：收紧采样区、或调 `MaximumColorDistance`、或改用无损格式跑离线标定（标定用 YUY2 慢速抓帧，运行用 MJPEG）。

> 注意最后这条折中方案：**标定阶段可以用低帧率无损格式**（标定是静态过程，不在乎帧率），**运行阶段才需要 MJPEG 的高帧率**。这可能是一条零成本的质量提升。

## Answer

（待填：实测采样率、阈值余量、是否需收紧采样区）
