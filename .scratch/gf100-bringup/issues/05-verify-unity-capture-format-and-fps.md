Status: ready-for-human
Type: task
Blocked by: 01, 02

# 05 验证 Unity 实际取帧格式与帧率、参数重连保持

规格见 [spec](../spec.md)。

## 问题

需求文档 04 节第 2 步写的是「在 Unity 用 `WebCamTexture` 接入实机」。但这里有一个实机才会暴露的陷阱：

**GF100 的 720p 在不同格式下帧率差 12 倍**（厂商参数表实测值）：

| 分辨率 | MJPEG 帧速 | YUY2 帧速 |
|---|---|---|
| 640×480 | 60 fps | 10 fps |
| 800×600 | 60 fps | 5 fps |
| **1280×720** | **60 fps** | **5 fps** |

而 **Unity 的 `WebCamTexture` 不暴露像素格式选择**，Windows 下由 Media Foundation 自行协商。一旦协商成 YUY2，Unity 里就是 5 fps，看起来像"相机坏了"。

## 待做

### T1 参数基线（先用 AMCap2，不写代码）

- 逐档切分辨率，抄下每档的**实际分辨率、帧率、传输格式**；
- 用 AMCap 的帧计数，数 10 秒求平均。

判据：720p **MJPEG ≥ 30 fps** 才可用。

### T2 Unity 取帧验证

写最小场景：`new WebCamTexture(deviceName, 1280, 720, 60)`，在 `didUpdateThisFrame` 里计数，跑 10 秒。

判据：
- ≥ 30 fps → 可用，进入 T3；
- ≈ 5 fps → 落到了 YUY2，按下面顺序尝试备选路径。

备选路径（成本递增）：
1. 设 `requestedFPS = 60` 且只请求 1280×720，看是否推动 MF 选 MJPEG；
2. 改用 Media Foundation / OpenCV 直接取流，把帧上传成 Unity 纹理或走共享内存；
3. 记录下来，作为评估换全局快门相机的依据。

### T3 参数锁定与重连保持

1. 关自动曝光、自动白平衡、自动增益；
2. 按"曝光策略"调到不过曝（见 `output/GF100_落地注意事项.md` 第五节）；
3. **记录数值**（曝光值、增益、白平衡色温）；
4. 拔掉 USB 重插，看参数是否保留。

判据：需求文档 01 节要求「相机重连后按记录复核或恢复」。若不保留，需在软件启动时用 UVC 写接口重设——这依赖 issue 02 的"可写"结论。

## Answer

（待填：T1 各档格式/帧率表；T2 Unity 实测 fps；T3 重连保留与否）
