# 采集 JSON 中的相机配置

更新日期：2026-10-19。适用于任务 A 的 `SimulationCameraCapture`。

## 使用方式与文件关系

在当前仿真场景点击“保存 RGB + 标注”，仍会生成同名 PNG 和 JSON。相机元数据在顶层 schema 2 引入，当前随光照扩展升至 **3**；`cameraConfigurationId` 和 `cameraConfiguration` 的内容与编号算法保持相机 schema 1。schema 3 新增的光照字段见 [光照使用与代码说明](task-a-lighting-guide.md)。历史文件不会被重写，读取器需要允许 schema 1 缺少相机字段、schema 1/2 缺少光照字段。

代码读取实际采集相机的参数，正交、透视均可使用。它不会替你切换投影模式或调整机位。图像尺寸来自相机绑定的 RenderTexture，与 Game 窗口尺寸无关。

- `Assets/Scripts/Simulation/SimulationCameraCapture.cs`：配置快照、编号、配对导出及一致性保护。
- `Tests/Unity/SimulationCameraMetadataChecks.cs`：在隔离 Unity 工程中实际渲染和读取 PNG/JSON 的检查源码，不参与主工程 .NET 检查项目的编译。
- `Tests/RunSimulationCameraMetadataChecks.ps1`：将本次 Simulation 源码和检查源码复制到 `.scratch/camera-metadata-validation/run-*`，运行隐藏的独立 Unity 工程；主工程可以保持打开。
- `docs/research/simulation-camera-metadata-github.md`：编码前候选、源码和许可证比较。本次没有引入外部包或复制第三方代码。

## 字段含义

| 字段 | 内容与单位 |
| --- | --- |
| `cameraConfigurationId` | `camera-v1-` 加 SHA-256 十六进制字符串。用于区分记录的相机配置。 |
| `cameraConfiguration.schemaVersion` | 相机配置自身的结构版本，当前为 1；独立于顶层 GT JSON 版本。 |
| `unityVersion` | 生成配置的 Unity 版本，同时纳入配置编号。 |
| `projectionType` | `orthographic` 或 `perspective`。 |
| `imageWidth/imageHeight` | 导出 PNG 的像素宽高。 |
| `worldPosition` | 相机世界位置，Unity 世界单位。 |
| `worldRotation` | 相机世界旋转四元数，分量为 x/y/z/w；不是欧拉角。 |
| `verticalFieldOfViewDegrees` | 垂直 FOV，单位为度；透视成像使用。 |
| `orthographicSize` | 正交画面的半高度，Unity 世界单位；正交成像使用。 |
| `aspect` | 相机当前的宽高比。 |
| `nearClipPlane/farClipPlane` | 近、远裁剪距离，Unity 世界单位。 |
| `viewport` | 相机配置的归一化 x/y/width/height。 |
| `usePhysicalProperties` | 是否使用 Physical Camera 属性。 |
| `focalLengthMillimeters` | 焦距，毫米；应结合 Physical Camera 开关解释。 |
| `sensorSizeMillimeters` | 传感器尺寸 x/y，毫米。 |
| `lensShift`、`gateFit` | 当前镜头偏移和 Gate Fit 配置。 |
| `worldToCameraMatrix` | Unity 世界到相机空间的 4×4 矩阵。 |
| `projectionMatrix` | Unity 的 4×4 投影矩阵，包含自定义投影矩阵时的实际值。 |

矩阵数组均有 16 项，按行排列：`values[row * 4 + column]`。列向量计算顺序是 `projectionMatrix * worldToCameraMatrix * worldPoint`。Unity 的这个相机空间以 **-Z 为前方**；这些数组不是 OpenCV 的 3×3 像素内参 K，也不能直接当作采用 +Z 前方约定的外参。

## 配置编号与采集过程

1. `SnapshotCamera()` 在渲染前读取世界姿态、输出尺寸、投影参数和矩阵，保存为自有的可序列化字段容器。
2. `Finite()` 拒绝 NaN/Infinity，并把负零归一为零；`MatrixValues()` 明确矩阵排列顺序。
3. `ConfigurationId()` 对配置容器的紧凑 `JsonUtility.ToJson` UTF-8 内容计算 SHA-256。固定字段、配置 schema 和 Unity 版本共同限定编号的生成规则；其他语言应读取保存好的 ID，不必自行用另一种 JSON 序列化器重算。
4. `SaveImage()` 调用现有相机渲染和图像读回，随后再次读取配置。如果配置、布局版本或输出 RenderTexture 对象改变，本次采集被拒绝，不发布混合来源的 PNG/JSON。
5. 配对写入继续使用临时文件，最后发布 JSON；一对文件共用同一 `frameId`。

重复采集、改变积木布局、时间和文件名都不会进入相机编号；更改记录内的相机参数会生成不同编号。返回同一份记录配置时编号恢复一致。未启用的 FOV/正交尺寸等配置值仍被记录，因此修改这些值也可能改变编号。

编号覆盖的是**记录的相机几何配置**。光照、材质、噪声、曝光和外部后处理不在本次配置内；同编号不表示这些成像条件相同，也不代表真实设备已经标定。

## 给 B/C 的信息

B 可使用图像、帧号、公开的棋盘几何和相机配置选择标定方法。JSON 内的 `cells` 与真实 `brickId` 仍只供评价，不作为 RGB 识别的答案输入。坐标变换和顶面高度差如何处理由 B 负责；本次没有替 B 定义识别算法或 B→C 预测接口。

## 验证方法

在仓库根目录执行：

```powershell
./Tests/RunSimulationCameraMetadataChecks.ps1
```

可用 `-UnityPath` 指定同版本编辑器路径。结果写入命令输出的独立工程目录中的 `verification.json`，并保留正交、透视及参数变化的 PNG/JSON 样本。检查包含实际 PNG 解码尺寸、已有 GT/角点字段、矩阵排列、重复/跨实例/跨区域性的编号稳定、参数变化，以及渲染回调改变配置/布局/目标纹理时的拒绝行为。

此检查验证元数据与采集的一致性，不是透视定位误差、独立测试点标定精度或真实设备效果的验收。

### 2026-10-19 验证结果

Unity 6000.3.24f1 独立工程实际运行通过 **684 项断言**，生成 **15 对 PNG/JSON**。覆盖正交、透视、Physical Camera 配置、非正方形分辨率、跨相机实例及区域性、参数恢复，以及采集期间配置变化的拒绝行为。

- [完整结果](../.scratch/camera-metadata-validation/run-20261019-201359-380/verification.json)
- [透视配置示例](../.scratch/camera-metadata-validation/run-20261019-201359-380/perspective.json)
- [正交配置示例](../.scratch/camera-metadata-validation/run-20261019-201359-380/orthographic.json)

证据位于本地 `.scratch`，不自动随正式代码发布；运行上述脚本可重新生成。本次没有操作主工程编辑器的 Play 状态或保存其未保存场景。
