# 手机手势与正交缩放：GitHub 检索与有限借鉴

日期：2026-10-11；本记录在运行时代码修改前完成。

## 范围与验收

用户要求说明手机操作并加入缩放。地图页电脑右键旋转、滚轮缩放；手机单指轻点选出生点、单指拖动旋转、双指捏合缩放。保留点击按钮入口。缩放仅调整正交尺寸，近远同尺寸积木的显示大小保持一致。拖动、捏合、触摸取消不能误选出生点；从 UI 发起的手势和照片页输入不能操作棋盘；多指退回单指需全部抬起后重新开始，避免突变。

首版需求的 Android 离线和出生点规则保持不变。不新增包或修改输入系统。Android 构建模块本机缺失，须如实区分编辑器手势帧验证与 APK 真机验收。

## 检索及实际阅读

搜索关键词：`site:github.com Unity orthographic camera pinch zoom touch drag MIT`、`site:github.com Jermesa JrsOrbitCamera touch zoom`。

| 候选、实际阅读 | 授权、匹配点、兼容与维护证据 | 采用判断 |
| --- | --- | --- |
| [Jermesa-Studio/smooth-third-person-vehicle-orbit-camera-script-for-unity](https://github.com/Jermesa-Studio/smooth-third-person-vehicle-orbit-camera-script-for-unity)，main 的 README、[JrsOrbitCamera.cs](https://github.com/Jermesa-Studio/smooth-third-person-vehicle-orbit-camera-script-for-unity/blob/main/JrsOrbitCamera.cs)（150 行，含完整 MIT 头） | MIT，Copyright (c) 2023 Samborlang Pyrtuh。读取两指前后距离、滚轮、缩放 clamp 与轨道计算。独立 UnityEngine/Legacy Input，不依赖运行时网络，Android 可行性来自源码分析；Unity 6 与真机须本项目验证。仓库此前列出 5 次提交，没有取得固定提交号。 | 已有轨道计算继续复用；本次有限改编前后两指距离计算。改为正交尺寸的比例缩放；一指静止时另一指移动也有效，且双指操作不同时旋转。保留 MIT 头。放弃跟车距离、协程、Input 轴与限定屏幕中间区域。 |
| [WobiWanKenobi/orthographic-camera-controller](https://github.com/WobiWanKenobi/orthographic-camera-controller)，main 的 README、[CameraController.cs](https://raw.githubusercontent.com/WobiWanKenobi/orthographic-camera-controller/main/CameraController.cs) | 正交相机，以 orthographicSize 缩放，与方向分离；Legacy Input，无离线网络依赖，Android 是源码推断。仓库列出 11 次提交，最近日期未取得。页面标注 MIT，但 LICENSE 正文抓取未成功。 | 参考正交尺寸缩放机制，沿用现有相机接口；不复制其代码、WASD/平移/rig 和 Input 轴。授权正文未取得不作为代码采用依据。 |
| [sergane13/Camera-Movement-By-Touch](https://github.com/sergane13/Camera-Movement-By-Touch)，README 和 [License.txt](https://raw.githubusercontent.com/sergane13/Camera-Movement-By-Touch/main/License.txt) | README 有轻点/拖动区分、捏合、UI 忽略，声明 Unity 2021.2/旧输入；根目录列出 46 次提交。README 标 BY-SA 4.0，但许可证实际是 BY-NC-SA 4.0，二者不一致；Assets 树和猜测的源文件路径未成功提供源码。 | 只记录比较线索，不采用源码；无法完整核对实现且授权不一致。本次使用上面已读且有 MIT 头的实现作为改编依据。 |

核对 Unity 6 的 [Input.GetTouch](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Input.GetTouch.html)、[simulateMouseWithTouches](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Input-simulateMouseWithTouches.html) 和 [Camera.orthographic](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Camera-orthographic.html)。保留触摸模拟鼠标以支持现有 IMGUI 按钮，在地图拾取和鼠标旋转/滚轮路径屏蔽触摸产生的重复事件。

## 整合方案

- BoardOrbitCamera 保存相对缩放倍率，范围 0.65—2.5，默认 1；每个滚轮单位或按钮步长按 1.15 比例缩放，Apply 使用默认适配尺寸除以倍率。旋转与每帧更新不覆盖缩放，Reset 同时恢复角度与 100%。放大围绕棋盘中心，放大后允许只看局部，复位恢复全图。
- LevelView 提供缩放及只读倍率入口。DemoUI 增加滚轮、减/加按钮及倍率显示；手机提示单指拖动和双指捏合。
- 提取同一份 BoardUiLayout 供绘制及触摸命中判断使用，防止两条输入路径的 UI 边界不一致。
- BoardTouchControls 处理手势生命周期，跟踪 fingerId，采用已读 MIT 距离计算子集；首次两指接入不缩放，不随触点顺序切换跳变。单指按屏幕尺寸归一化旋转，轻点只在抬起时确认；拖动阈值按短边尺寸适配，经过捏合后需全指抬起才能再次轻点/旋转。
- 从 UI 起始、任一捏合触点在 UI、越界、三指、取消、失焦或照片页打开时阻止/结束棋盘手势。允许已经发起的单指旋转经过 UI；结束在 UI 上不会选格。原始触摸坐标转换为左上角 GUI 坐标，再使用现有 ScreenPointToRay 拾取。
- 使用当前 Legacy Input 的 GetTouch 读取帧，不创建 Input.touches 数组，不改全局触摸模拟鼠标开关。

## 验证计划

扩展既有隔离 Unity Game 视图检查：真实滚轮事件、按钮、边界、Reset、旋转/窗口变化后的倍率保留与缩放后拾取；通过实际输入处理入口注入 Touch 帧，覆盖轻点、拖动、双指张开/收拢及一指静止、fingerId 顺序变化、两指转单指、UI/照片页、取消/失焦和模拟鼠标的重复选择。保存宽窄屏缩放实际画面。手机 APK 的触摸、手感和设备差异另做真机验收，不以注入帧冒充真机结果。

## 实施结果

按上述方案完成有限改编，Jrs MIT 声明保留在 BoardTouchControls；WobiWanKenobi 和 sergane13 不采用代码。无新包或输入系统修改。Unity 编译和最终隔离检查退出 0，滚轮/按钮/边界/复位、缩放后选格、手势帧与 UI/照片页隔离通过；远近等尺寸仍成立。实际截图和验证范围见 [.scratch/touch-zoom/spec.md](../../.scratch/touch-zoom/spec.md)。本机无 Android 构建模块，原生真机测试待后续进行。

验证中核对官方 [GameViewSize.cs](https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/master/Editor/Mono/GameView/GameViewSize.cs) 和 [GameViewSizes.cs](https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/master/Editor/Mono/GameView/GameViewSizes.cs) 的内部接口，处理 Free Aspect 瞬时高度变化。只以反射调用当前本机接口，不复制 Unity Reference Only 授权的源码；尺寸变更仅存在于隔离进程内存，退出前恢复，不新增或保存全局尺寸配置。
