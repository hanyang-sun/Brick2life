# Third-party notices

## Noto Sans SC 中文界面字体

- 来源：[notofonts/noto-cjk](https://github.com/notofonts/noto-cjk)，树版本 `f8d157532fbfaeda587e826d4cd5b21a49186f7c` 的 `Sans/SubsetOTF/SC/NotoSansSC-Regular.otf`；内部字体版本 2.004。
- 本地资源：`Assets/Resources/Fonts/NotoSansSC-Regular.otf`，未修改字体二进制、名称或字形。字体 metadata 版权为 © 2014-2021 Adobe (http://www.adobe.com/).
- SIL Open Font License 1.1 完整文本见 `Assets/ThirdParty/NotoSansSC/OFL.txt`，播放器随包许可证为 `Assets/Resources/Fonts/NotoSansSC-OFL.txt`；来源与 SHA-256 见同目录 SOURCE.md。
- 用于地图、战斗和扫描界面的中文 GUIStyle/输入框。3d-group/unity-simple-localization 仅作为集中错误文案查找机制参考，未采用其源码或包。检索及具体整合见 `docs/research/chinese-ui-github.md`。

## Roy-T.AStar 最小堆

- 来源：[roy-t/AStar](https://github.com/roy-t/AStar)，提交 `3d57efaa83c8cd217e4973fd4738c57e2f694bd2`。
- 原文件：[Roy-T.AStar/Collections/MinHeap.cs](https://github.com/roy-t/AStar/blob/3d57efaa83c8cd217e4973fd4738c57e2f694bd2/Roy-T.AStar/Collections/MinHeap.cs)。
- 本地文件：`Assets/ThirdParty/RoyTAStar/MinHeap.cs`，直接下载原文件，未修改实现。
- 授权：MIT，Copyright (c) 2017 R. A. Triesscheijn。完整声明保存在 `Assets/ThirdParty/RoyTAStar/LICENSE.txt`。
- 原文件声明参考 [robin-thomas/min-heap](https://github.com/robin-thomas/min-heap)。上游 MIT 声明（Copyright (c) 2014 Robin Thomas）保存在 `Assets/ThirdParty/RoyTAStar/UPSTREAM-LICENSE.txt`，读取版本为 `a1a8d7137f3afdf2b5ebf93b9d4059c4d1dd96e8`。
- 采用范围：`GridPathfinder` 的待搜索候选队列，仅调用 `Insert`、`Extract`、`Count`。四方向、单位代价、多核心与等距选择规则由本项目适配。

思路参考而未采用源码或资产的项目，及各候选的比较，见 `docs/research/existing-milestone-github.md`。

## Colourful 色彩转换子集

- 来源：[tompazourek/Colourful](https://github.com/tompazourek/Colourful/tree/9c9a2c80a28b4108fc00f2b87ebc18bf38cea66c)，提交 `9c9a2c80a28b4108fc00f2b87ebc18bf38cea66c`。
- 本地文件：`Assets/ThirdParty/Colourful/NormalizedLab.cs`。MIT，Copyright (c) 2023 Tomáš Pažourek；完整授权保存在同目录 `LICENSE.txt`。
- 改编 `sRGBCompanding.ConvertToLinear`、`XYZToLabConverter.Convert` 的数值子集，固定 sRGB/D65 矩阵及白点，省略上游转换器、色域和矩阵构建框架。
- RGB 最大通道归一化、弱亮度权重距离、暗部过滤以及无格纸的低饱和明度判断属于本项目适配；该距离不是标准 Delta E。
- colour-science/colour 与 OpenCV 仅作研究参考，不引入其源码或依赖。候选比较和固定源码链接见 `docs/research/color-recognition-github.md`。

## JrsOrbitCamera 轨道计算子集

- 来源：[Jermesa-Studio/smooth-third-person-vehicle-orbit-camera-script-for-unity](https://github.com/Jermesa-Studio/smooth-third-person-vehicle-orbit-camera-script-for-unity)，2026-10-11 读取的 `main` 分支 [JrsOrbitCamera.cs](https://github.com/Jermesa-Studio/smooth-third-person-vehicle-orbit-camera-script-for-unity/blob/main/JrsOrbitCamera.cs)（150 行源文件快照）。抓取未取得固定提交号。
- 本地文件：`Assets/Scripts/Presentation/BoardOrbitCamera.cs`，局部改编角度累计、俯仰限制、Quaternion 旋转固定半径的相机定位；`BoardTouchControls.cs` 有限改编两指前后距离计算。两个文件均保留完整 MIT 声明，Copyright (c) 2023 Samborlang Pyrtuh。
- 适配固定棋盘中心、水平角循环、45°—75°俯仰、55°默认桌游视角复位及正交投影与尺寸。两指距离改为比例缩放正交尺寸，允许一指静止，双指不同时旋转。IMGUI 输入、手势生命周期、UI 隔离及按钮属于本项目衔接；不采用车辆跟随、缩放协程或 Input 轴。
- Cinemachine 与 unity-cheat-sheet 仅参与比较，不引入其源码或包。详细阅读与采用依据见 `docs/research/orbit-camera-github.md`。
- 本次缩放比较中的 WobiWanKenobi 和 sergane13 不采用源码或资产，见 `docs/research/touch-zoom-github.md`。

## Brackeys 塔防战斗计算与流程子集

- 来源：[Brackeys/Tower-Defense-Tutorial](https://github.com/Brackeys/Tower-Defense-Tutorial)，`master` 分支于 2026-10-11 阅读的 `WaveSpawner.cs`、`EnemyMovement.cs`、`Enemy.cs`、`Turret.cs`。未取得固定提交号；未引入上游素材或工程依赖。
- 本地 `Assets/Scripts/Domain/BattleSimulation.cs` 局部适配路点索引、最近目标/射程、攻击倒计时和死亡保护，改为纯 C#、固定步长、共享核心、手动波次、地图快照和即时伤害。来源与比较见 `docs/research/combat-a-github.md`。
- RyanJiff 模板只作为双条件波次完成的流程参考，不采用其源码或资产。
- 多入口轮流分配参考 Unity Boss Room 的累计计数机制，生成器职责参考 Unity Onslaught；仅作机制参考，未复制/改编源码或引入资源与依赖。完整比较见 `docs/research/multiple-spawns-github.md`。

上游 LICENSE.md 全文（Unlicense）：

```text
This is free and unencumbered software released into the public domain.

Anyone is free to copy, modify, publish, use, compile, sell, or
distribute this software, either in source code form or as a compiled
binary, for any purpose, commercial or non-commercial, and by any
means.
In jurisdictions that recognize copyright laws, the author or authors
of this software dedicate any and all copyright interest in the
software to the public domain. We make this dedication for the benefit
of the public at large and to the detriment of our heirs and
successors. We intend this dedication to be an overt act of
relinquishment in perpetuity of all present and future rights to this
software under copyright law.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.

For more information, please refer to <http://unlicense.org>
```

## Unity UI（uGUI）2.0.0

- 来源：Unity 6000.3.24f1 随附的官方内置包 `com.unity.ugui`，在 `Packages/manifest.json` 中声明版本 2.0.0。
- 上游源码：https://github.com/Unity-Technologies/uGUI 。本项目通过 Package Manager 使用原版包，没有复制或修改包源码。
- 授权：Unity Companion License for Unity-dependent projects；Copyright © 2015-2020 Unity Technologies ApS。完整许可证入口和声明随该包的 `LICENSE.md` 保留。
- 使用范围：仿真场景的 Canvas、RawImage、比例布局、工作台文字与鼠标格位提示的四条 Image 边线，显示 TopCameraRGB；研究比较见 `docs/research/simulation-camera-preview-github.md`、`docs/research/simulation-workspace-view-github.md` 与 `docs/research/simulation-grid-pointer-github.md`。Cinemachine 和 Sunny Valley Studio 网格系统仅作机制比较，没有引入其源码或包。
