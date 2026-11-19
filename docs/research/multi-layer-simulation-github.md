# 多层积木摆放的 GitHub 调研

2026-11-18 在 GitHub 检索 `Unity C# 3D grid building placement`、`stacked blocks grid cell height`、`Unity voxel editor place block raycast`。范围是默认仿真场景的多层放置、移动、删除和搭建视角。验收包含三层以上堆叠、禁止悬空和重叠，以及现有公开接口与 GT 字段兼容。

| 项目 | 查阅内容与授权 | 适用性 |
| --- | --- | --- |
| [Grid Placement System Unity 2022](https://github.com/SunnyValleyStudio/Grid-Placement-System-Unity-2022) | [README](https://github.com/SunnyValleyStudio/Grid-Placement-System-Unity-2022#readme)、[GridData.cs](https://github.com/SunnyValleyStudio/Grid-Placement-System-Unity-2022/blob/main/GridData.cs)、[MIT](https://github.com/SunnyValleyStudio/Grid-Placement-System-Unity-2022/blob/main/LICENSE) | `GridData` 在创建物件前校验格位占用，数据和场景对象分开。现有脚印逻辑只扩展 X/Z，没有支撑规则。Unity 2022 教学示例，依赖少。核心 C# 逻辑可离线运行，Android 未验证。 |
| [Unity Voxel Engine](https://github.com/Seremonik/Unity-Voxel-Engine) | [README](https://github.com/Seremonik/Unity-Voxel-Engine#readme)、[IVoxelsGenerator.cs](https://github.com/Seremonik/Unity-Voxel-Engine/blob/main/Runtime/Scripts/IVoxelsGenerator.cs)、[MIT](https://github.com/Seremonik/Unity-Voxel-Engine/blob/main/LICENSE) | Unity C# 生成接口按三维区块组织体素。该项目有区块生成和性能优化实现，引入 Jobs/Burst 的成本超出 10×10 棋盘需求。机制可离线运行，插件的 Android 支持未验证。 |

借鉴格位占用与场景对象分开的机制，以及用独立高度索引表示每块积木的方式。沿用项目现有的 `BoardLayout` 和 Unity 射线拾取，采用每格连续堆叠的支撑规则。层操作留在仿真内部，公开接口和底层快照语义保持一致。GT 继续提供原有底层 `cells`，用新增的 `bricks` 导出所有积木及层号。只参考设计思路，未引入外部实现或依赖。

## 搭建相机旋转

2026-11-18 检索 `Unity camera orbit RotateAround drag around target`。阅读 [UnityCsReference 的 Transform.RotateAround](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Transform/ScriptBindings/Transform.bindings.cs) 和 [Reference-Only 许可](https://github.com/Unity-Technologies/UnityCsReference/blob/master/LICENSE.md)，以及 [Cinemachine 轨道相机说明](https://github.com/Unity-Technologies/com.unity.cinemachine/blob/main/com.unity.cinemachine/Documentation~/FreeLookCameras.md)、[Orbital Follow 实现](https://github.com/Unity-Technologies/com.unity.cinemachine/blob/main/com.unity.cinemachine/Runtime/Components/CinemachineOrbitalFollow.cs) 和 [Unity Companion 许可](https://github.com/Unity-Technologies/com.unity.cinemachine/blob/main/com.unity.cinemachine/LICENSE.md)。前者提供 Unity 内置 C# API，后者提供更多跟随和输入配置并需要引入新包。两者可在本地运行，触屏操作不在此次范围。

复用原有摆放相机、棋盘取景和投影展示窗口的左键旋转方式，调用内置旋转 API 绕棋盘中心水平转动。摆放窗口用拖动距离区分点击与旋转，移动工具从积木起拖时优先移动积木。验收为旋转后仍能正确选中、拖动和删除三层内的积木。采集相机继续使用固定机位。两份参考均未引入外部源码或依赖。
