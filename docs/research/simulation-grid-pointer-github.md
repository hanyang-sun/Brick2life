# 鼠标格位定位与屏幕格框：GitHub 比较与有限借鉴

日期：2026-10-16，Asia/Shanghai。先完成本记录，再编写本步运行代码。

## 范围与验收

依据当前任务 A 规格和三维工作台实施顺序：鼠标悬停在棋盘或单位积木上时，显示固定 gridX/gridY 格号和黄色格框；单层 layer=0。棋盘以 X/Z=0～10、Y=0 为基准，格号 0～9。鼠标位于棋盘外、屏幕外或右侧固定相机预览时清除反馈。暂不添加点击编辑、工具模式、移动交互、GT 导出、识别或投影。

实验纹理应保持原样，格框只是操作提示。正式新增一个运行脚本，沿用已有 uGUI 2.0.0、Legacy Input 和字体。

## 检索与实际阅读

关键词：`site:github.com SunnyValleyStudio Grid Placement System Unity InputManager GetSelectedMapPosition LICENSE`、`site:github.com Unity grid placement ScreenPointToRay raycast floor mouse license MIT`。

| 候选与实际读取 | 授权、兼容性与差异 | 采用方式 |
| --- | --- | --- |
| [SunnyValleyStudio/Grid-Placement-System-Unity-2022](https://github.com/SunnyValleyStudio/Grid-Placement-System-Unity-2022)：main 的 [README](https://github.com/SunnyValleyStudio/Grid-Placement-System-Unity-2022/blob/main/README.md)、[InputManager.cs](https://github.com/SunnyValleyStudio/Grid-Placement-System-Unity-2022/blob/main/InputManager.cs)、[PreviewSystem.cs](https://github.com/SunnyValleyStudio/Grid-Placement-System-Unity-2022/blob/main/PreviewSystem.cs)、[LICENSE](https://github.com/SunnyValleyStudio/Grid-Placement-System-Unity-2022/blob/main/LICENSE) | MIT，Copyright (c) 2023 Peter。README 面向 Unity 2022；InputManager 使用显式 Camera、旧 Input、Physics.Raycast 和 EventSystem，与当前 Legacy Input 相符。未命中时返回缓存位置，会在本项目造成格框停留；PreviewSystem 使用世界物体/材质，会进入实验 RGB。没有引入本机验证过的版本，不把标题版本等同于 Unity 6 已验收。运行依赖无网络；Android 离线能力仅作依赖判断，未真机验证。 | 只参考“输入→操作相机射线→命中”和反馈与输入分离的机制，独立编写本项目代码。失败立即清除格位，显式排除侧栏；积木侧面命中以单位 Cube 的 bounds.center 确定其列。保留格框在 UI 中，不采用世界预览物体、缓存命中位置、建筑数据库或完整 Grid 系统。不复制源码、不增加包。 |
| [Unity-Technologies/uGUI](https://github.com/Unity-Technologies/uGUI)：main 的 com.unity.ugui/README.md、LICENSE.md、Runtime/UGUI/UI/Core/Image.cs、Graphic.cs 中的射线开关，以及 UnityCsReference 的 RectTransformUtility 屏幕点转局部点实现；另读本机 uGUI 2.0.0 README、LICENSE 和 Image 的组件约束。 | 官方 UI 项目，Unity Companion License。本机已直接依赖 2.0.0。Image 的 RectTransform 可表示旋转后的细长矩形；Overlay Canvas 的转换使用 null 相机。与上个候选的世界标记相比，屏幕 UI 不进入 TopCameraRGB。上游 main 只用于机制核对，不当成本机包版本；本轮无新增原生、平台或网络依赖。 | 直接使用现有包中的四个 Image 和一个 Text。将格子四角投到屏幕，再转换到同一个 UI 父矩形坐标；每条边以中点、长度及角度定位。所有新 UI 的 Raycast Target=false。不继承 Graphic、不复制 UI 源码，也不引入额外绘线库。 |

另检索 ABIKARTHICKGDEV/Unity-Mouse-Position-Examples；实际取得仓库 README，但独立源文件和 LICENSE 抓取失败，因此不作为代码采用依据。SinlessDevil 仓库本轮 raw 请求也失败，不将历史记录替代本次实际读取。

以上为读取日期的 main 快照，未取得固定提交号。候选比较完成于本步编码前。

## 本项目适配

1. 新增 SimulationGridPointer，显式引用 WorkspaceCamera、Board Collider、BricksRoot、格位 Text、格框根和四条 UI 边；不使用 Camera.main。
2. 先检查屏幕点位于操作相机 pixelRect 内；未来存在 EventSystem 时再排除其指向的交互 UI。当前不因只读悬停创建 EventSystem。
3. 从操作相机发射有限距离物理射线，只接受 Board 或 BricksRoot 下的 Collider，忽略 Trigger。
4. 命中棋盘时用命中点的 X/Z 向下取整；命中单位积木时用其包围盒中心的 X/Z 向下取整，避免最大侧面的边界坐标误指向邻格。格号必须在 0～9 内。
5. 在单层格子的表面高度显示格框；按相机投影和 Canvas 坐标更新四条 UI 边，预览始终保持原实验图像。这里是仿真编辑拾取，不将坐标传给识别模块。
6. 现有 Simulation asmdef 只增加实际使用的 UnityEngine.UI 程序集引用。没有引用旧塔防业务程序集、Contracts 或 UnityEditor。

官方接口核对：[ScreenPointToRay](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Camera.ScreenPointToRay.html)、[Physics.Raycast](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Physics.Raycast.html)、[Input.mousePosition](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Input-mousePosition.html)。旧 LevelView.TryPickCell 只与地面求交，且使用旧导航坐标，因此不直接接入当前棋盘。

## 验证计划

在当前主编辑器读取实际状态、保留场景备份，再停止 Play 配置。验证全棋盘格位、三块积木顶面/最大 X 侧面、四周边缘附近、棋盘外、屏幕外与侧栏。实际运行验证黄色格框与中文提示，改变窗口尺寸后继续核对拾取；悬停前后比较固定 RGB，不允许因高亮或标签而改变图像。保留截图和结果至 `.scratch/task-a-simulation/pointer-hover/`，完成后停止运行再将临时 Editor 工具移出 Assets。
