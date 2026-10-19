# 棋盘视角旋转：GitHub 检索与有限借鉴

日期：2026-10-11。本文在运行时代码修改前完成。

## 范围与验收

用户要求当前版本能转动视角。保持正交棋盘风格，围绕棋盘中心水平旋转并调整俯仰。桌面右键拖动；提供左右、俯仰及复位按钮。左键仍用于出生点选择，旋转不得改变地图、出生点或路线。UI 区域不能发起旋转；照片页暂停控制；旋转后地图拾取仍正确。限制俯仰，避免钻入桌面或翻转；不同窗口比例保持棋盘完整入框。

## 检索与实际阅读

限定 GitHub 搜索：`site:github.com Unity orbit camera mouse pitch clamp MIT`、`site:github.com Unity orbit camera orthographic license`。

| 候选及实际阅读文件 | 授权、维护证据与兼容性 | 采用判断 |
| --- | --- | --- |
| [Jermesa-Studio/smooth-third-person-vehicle-orbit-camera-script-for-unity](https://github.com/Jermesa-Studio/smooth-third-person-vehicle-orbit-camera-script-for-unity)，`main` 的 README 和 [JrsOrbitCamera.cs](https://github.com/Jermesa-Studio/smooth-third-person-vehicle-orbit-camera-script-for-unity/blob/main/JrsOrbitCamera.cs)（读取的文件为 150 行） | 源文件头含完整 MIT 声明，Copyright (c) 2023 Samborlang Pyrtuh。仓库页列出 5 次提交，抓取未提供最近日期；不能据此宣称持续维护或 Unity 6 已通过测试。独立 C#，使用 UnityEngine 和旧 Input，含鼠标及双指控制，运行时无网络调用。 | 采用其累计 yaw/pitch、俯仰限制、Quaternion.Euler 旋转固定距离并朝向目标的代码子集。输入改接当前 IMGUI，避免与左键选格冲突；不采用跟车、缩放协程及双指逻辑。Android 离线能力是源码依赖分析，须另行真机验证。 |
| [Unity-Technologies/com.unity.cinemachine](https://github.com/Unity-Technologies/com.unity.cinemachine)，README、[CinemachineOrbitalFollow.cs](https://github.com/Unity-Technologies/com.unity.cinemachine/blob/main/com.unity.cinemachine/Runtime/Components/CinemachineOrbitalFollow.cs)、[package.json](https://github.com/Unity-Technologies/com.unity.cinemachine/blob/main/com.unity.cinemachine/package.json)、[LICENSE.md](https://github.com/Unity-Technologies/com.unity.cinemachine/blob/main/com.unity.cinemachine/LICENSE.md) | 官方维护包；读取的 main 包声明 3.1.8-pre.2、Unity 2022.3，依赖 Splines 2.0.0 和 IMGUI。许可为 Unity Companion License。阅读球面轨道、水平角 wrap、垂直角范围与轴重置机制；这不等于验证本机 6000.3.24f1。 | 适合多目标跟随与镜头切换。本次固定棋盘只需要小范围适配；直接引入需新增包及相机管线配置。仅比较机制，不复制源码、不添加依赖。 |
| [ozankasikci/unity-cheat-sheet](https://github.com/ozankasikci/unity-cheat-sheet)，README、[camera-follow-orbit.md](https://github.com/ozankasikci/unity-cheat-sheet/blob/master/docs/practical-use-cases/camera-follow-orbit.md) | 读取了 AngleAxis、pitch clamp 与 LateUpdate 示例；根目录列表未提供 LICENSE，尝试 LICENSE 路径抓取失败，授权未确认。 | 仅作为检索线索，不采用代码。 |

Jrs 仓库独立 LICENSE 页面、raw URL 和提交 API 抓取失败，但实际源码头及 README 均包含完整 MIT 文本，因此授权依据是源文件头。记录使用读取日期及 main 分支快照，不虚构固定提交号。Unity 的 [GUIUtility.hotControl](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/GUIUtility-hotControl.html) 文档用于核对拖动捕获/释放规则。

## 整合方案（初版，后续调整见末节）

- 在 Presentation 增加 `BoardOrbitCamera`，局部改编 Jrs 的角度与相机定位计算；完整 MIT 声明保留在代码头，采用范围追加到 THIRD_PARTY_NOTICES.md。
- 由 LevelView 持有控制器，保存当前初始视角 `(23,32,-23)`，中心固定为棋盘中心；水平角循环，俯仰 25°—80°。保持现有正交尺寸随窗口比例变化。
- DemoUI 管理右键拖动与 GUI 捕获，按屏幕尺寸归一化位移；左右按钮每次转 45°，俯仰按钮每次调 10°，Reset 恢复初始视角。UI 阻止发起地图输入，拖动中允许经过 UI，抬起/离开窗口/失焦/打开照片页时结束捕获。
- Game 视图不会把所有出界事件转发给玩家 GUI，不能单靠 MouseLeaveWindow。利用项目现有的 Legacy Input（activeInputHandler=0），在 Update 中检查右键已松开或鼠标已出界，补充释放捕获；不读取 Input 轴。核对 [mousePosition](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Input-mousePosition.html) 和 [GetMouseButton](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Input.GetMouseButton.html) 的 Unity 6 公开文档后实施。
- 继续使用当前相机的 ScreenPointToRay 与地面平面拾取，网格坐标不随镜头变化。无新包、场景引用或输入设置修改。

## 验证计划

在已有隔离 Unity 工程中运行真实 Game 视图事件检查，验证水平/垂直旋转、角度限制、全周旋转、按钮/复位、UI 与照片页隔离、松开后停止、旋转后左键点选、例子切换和窄屏。截图包含 IMGUI。Windows 实测结果与 Android 待验证项分别记录。

## 初版实施结果（2026-10-11）

按以上范围局部改编 Jrs 的轨道计算并保留完整 MIT 声明。Unity 编译和 Game 视图事件检查通过（退出 0，ORBIT_CAMERA_CHECKS_PASSED）：旋转/俯仰/全周、五个按钮及复位、UI/照片页隔离、释放捕获、旋转后选格、示例切换和棋盘入框。这些按钮后来已移除；1280×801 与 540×801 的当时画面、验证范围与限制见 [验证记录](../../.scratch/orbit-camera/verification/README.md)。

## 斜俯视调整：编码前比较（2026-10-11）

用户反馈视角不舒服，要求没有近大远小，并选择能看到顶面和侧面的桌游视角。检查发现上一版已在 LevelView.Awake 中开启正交投影；本次加强运行时投影约束，并调整默认俯仰及允许范围。验收是同尺寸积木在不同深度具有相同屏幕尺寸，旋转和复位后仍满足此条件，同时保留选格、示例和扫描页行为。

检索关键词：`site:github.com Unity isometric orthographic camera rotation pitch camera controller`。阅读以下候选，不只依据搜索摘要：

| 候选与本次实际阅读 | 授权、兼容性与维护证据 | 判断 |
| --- | --- | --- |
| [WobiWanKenobi/orthographic-camera-controller](https://github.com/WobiWanKenobi/orthographic-camera-controller)，main 的仓库 README、[CameraController.cs](https://raw.githubusercontent.com/WobiWanKenobi/orthographic-camera-controller/main/CameraController.cs) | README 采用正交相机与 45°斜视 rig；源码读取 orthographicSize，以旋转 rig 改变朝向，缩放操作调整正交尺寸。UnityEngine 和旧 Input，无运行时网络依赖，Android 兼容性属于源码推断。仓库列出 11 次提交，最近日期未获取。仓库标注 MIT，但 LICENSE 正文与错误的 master 路径抓取失败。 | 参考保持正交尺寸与斜视方向分离的机制，沿用本项目已有相机接口。不会复制源码或新增其 WASD、缩放、rig 与 Input 轴逻辑；未获取许可证正文不作为代码采用依据。 |
| [Unity-Technologies/com.unity.cinemachine](https://github.com/Unity-Technologies/com.unity.cinemachine)，本次读 [LensSettings.cs](https://github.com/Unity-Technologies/com.unity.cinemachine/blob/main/com.unity.cinemachine/Runtime/Core/LensSettings.cs) 的 OverrideModes.Orthographic、OrthographicSize 和 ModeOverride；README、包元数据和许可证此前已读 | Unity Companion License，官方包；依赖及 Unity 版本信息见前表。源码将投影模式和镜头尺寸分别控制，能主动把正交模式应用到 Unity Camera。 | 参考明确应用投影模式的机制，将 orthographic=true 放入现有 Apply 路径；不引入包或复制源码。 |
| 已采用的 Jermesa-Studio/JrsOrbitCamera，main 的源文件与 MIT 头重新核对 | 独立 C# 轨道计算，授权与版本依据见前表。 | 继续使用已有有限改编；只调整本项目的默认值与俯仰上下限。 |

Unity [Camera.orthographic](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Camera-orthographic.html) 文档核对了投影开关及正交尺寸控制。具体方案在修改运行时代码前确定：默认 yaw -45°、pitch 55°；允许 pitch 45°—75°；每次 Apply 保证正交，Reset 恢复桌游视角。保持原正交尺寸和按钮步长。新增验证在默认、旋转后及窄屏视角下投影两块同尺寸积木，比较远近屏幕包围盒尺寸，并主动切换透视后检查 Apply 的恢复。先通过 Unity 编译和已有 Game 视图集成检查，再保存实际截图。

首次调整检查通过后，实际截图显示 55°与 75°斜视时底部浮动按钮遮住棋盘尖角。继续使用上述候选的 orthographicSize 控制机制，在第二次修改前确定将正交半高下限从 15.5 调为 21，保持宽度适配公式及按钮步长，使任意允许角度下棋盘为底部控件留出空间；正交尺寸不随旋转角度变化，避免旋转时整体缩放。验证增加棋盘包围盒在底部按钮/窄屏图例上方的检查。

### 桌游调整实施结果

按上述范围完成，仍只采用原有 Jrs 轨道计算子集，新增候选没有代码或包依赖。最终 Unity 编译及 Game 视图集成检查退出 0：主动切换透视后旋转和每帧更新恢复正交；默认、旋转及窄屏下远近同尺寸积木的屏幕尺寸差小于 0.01 像素；45°—75°限制、55°复位、既有输入/示例/照片页隔离及底部控件留白均通过。六张实际截图更新在 [verification](../../.scratch/orbit-camera/verification/README.md)，修改保存在本地。
