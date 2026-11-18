# 固定投影仪仿真

更新：2026-11-07。适用当前 Brick2Life-C 本地工作树、Unity 6000.3.24f1、Built-in 渲染管线、`Brick2LifeSimulation`。

## 当前效果

现有道路、章鱼塔、敌人、攻击和角色纹样仍由原代码生成，但这些面片只供输出图像使用，不再直接显示在积木上。虚拟投影仪从固定位置把这一张图像投到棋盘和积木表面，按表面颜色、纹理、法线方向和距离计算附加反射光。从投影仪视角生成的深度图决定哪些表面被前方物体遮住。

内容相机与虚拟投影仪共用位姿、视场角及裁剪范围，并用内容相机的实际矩阵回投输出图像。角色图案按已确认布局的目标表面放置，因此在投影仪可照到的范围内，移动镜头会重新生成预校正输出，图案仍对准目标；高积木或其他不透明挡光物仍会形成投影阴影。黑色输出默认不增加光，不会像黑色贴纸一样盖黑积木。相同白光在红、绿积木上产生不同颜色的反射贡献。

测试接口 `CalibrationPattern` 直接使用固定幻灯片，绕过内容烘焙：移动投影仪或改变接收面高度仍会移动光斑。它用于独立检查光路，与已校正的角色内容不同。

搭建窗口继续保持无投影，作为编辑观察视图。顶置 RGB 与展示窗口接收表面投影光；展示窗口沿用已确认布局重建积木，不改变其版本与只读规则。确认前，预览积木与源场景真实几何可能短暂不同；真正用于 B 识别的图像始终来自源场景。仅投影模式保留为平面诊断显示，隐藏积木后不用于验证实物遮挡。

## 使用与调节

重新进入 Play 即自动启用，无需重新运行安装菜单。选择 Hierarchy 中的 `SimulationFeedback`，查看 `Simulation Projected Light` 组件：

| 参数 | 默认值 | 含义 |
| --- | --- | --- |
| Projector Position | `(5,14,5)` | 世界坐标中的镜头位置 |
| Projector Angles | `(90,0,0)` | 默认垂直向下；改变后产生斜向投影 |
| Field Of View | 约 `46.4°` | 输出覆盖范围，默认棋盘平面约 12×12 单位 |
| Output Resolution | `512` | 正方形输出及遮挡图分辨率 |
| Light Strength | `1.3` | 投影光相对强度，过强会产生饱和 |
| Black Level | `0` | 理想黑场；可调非零值模拟残余光，未按实机校准 |
| Blur Pixels | `0.5` | 图像的近似模糊半径，不是已标定镜头模型 |
| Shadow Bias | `0.025` | 避免表面自身误遮挡的世界距离容差 |

Play 中的参数调整用于实验，退出 Play 后 Unity 不自动保存这些运行时改动。若要保存默认参数，可在退出 Play 后为 `SimulationFeedback` 添加同名组件并设置，运行时会复用它；安装菜单也会添加该组件。

章鱼脸位于已确认积木顶面（当前固定 `y=1`），触手、道路、射程圈和入口位于棋盘（`y=0`）。删除旧贴面高度偏移及其场景/安装配置，透明图案的叠放改用绘制排序。内容相机只绘制图案层，不读取真实几何决定内容位置；真实几何只参与接收光与遮挡。积木移动而识别尚未确认时，内容仍在旧目标位置。当前单层识别合同没有改变；用于验证光路的高物体不表示已完成多层识别，未来仍须从识别结果获得高度才能校正。

地面路线采用洋流航道。准备时显示较淡的路径，战斗中沿实际导航路线流动水纹和浅青色箭头。拐弯在格内做圆角，共用路段合成到同一张图案中，入口使用旋涡图标。图案在路线变化时重算，暂停时保持当前流动位置。选型和验证见 [洋流航道记录](research/ocean-current-projection-github.md)。

## RGB 输入与暂停边界

当前已整合 Brick2Life 的光度恢复与归一化 RGB 分类算法。默认直接对实际带投影 RGB 使用 `ProjectedLightRecognizer`，暂停采集使用 `FixedGridRecognizer`。顶部按钮/F8 仅切换“连续识别 / 暂停识别”，保留当前确认布局；待确认计数重新开始，不把同一照片重复计作多帧。投影的几何、光强、角色动画、遮挡及搭建视图保持 C 的实现。

为避免 C 的高亮图案在识别相机中提前裁剪，相机使用运行时浮点缓冲和固定 `captureExposure=0.3`，在最后输出到 RGB 前统一乘曝光系数；相机预览与识别来自相同结果，展示窗口亮度不变。曝光在 Bind 时锁定，退出后恢复原 RenderTexture 格式/HDR 状态。导出 JSON 相机配置增加 `exposureScale`；需要改默认曝光时退出 Play 后设置，再进入 Play。

补偿从本次实际输出纹理逐采样点读取光照，包括角色、敌人、道路、模糊、黑电平和已知投影位姿；顶面与桌面假设使用同一张 RGB，没有隐藏的干净布局图，也不读取真实占用、颜色、ID、材质或深度。已知 0.5 灰参考面在独立离屏相机中标定环境响应，光照/相机配置改变时更新；它没有玩家积木、没有投影，也不参与显示或物理交互。针对环境阴影，算法从 RGB 验证中性棋盘的衰减，而不是读取遮挡真值。

当前响应适配针对 Gamma 管线，原算法的实机 `Fit`/响应指数 2.2 接口保留；仿真使用指数 1 匹配 C 的颜色叠加，不能把此参数直接当作实机标定。输出按瞬时虚拟曝光同步，旧帧不能搭配当前投影纹理；硬件快门积分、设备延迟和真实响应仍未验证。过曝或无法解释的像素仍为未知，保留上次确认结果。强烈镜面反射、复杂遮挡或整体光照变化下的普适准确率不作保证。

原 Brick2Life 的离屏干净图→`PhotometricComposer`→识别合成图流程没有迁入运行时。合成器仅保留在 `Tests/PhotometricComposerFixture.cs` 作数学回归；未迁入旧的逐格 ProjectionPlan 光照估计、三维替身或暂停同图重复确认。

31 层现在是“投影内容源”，只在输出图像生成期间绘制。观察相机即使排除该层，也不能消除已经照在表面上的光。F8 只切换连续/暂停识别；选择暂停模式本身不熄灭投影，按暂停后才由统一门持续关闭真实投影贡献，继续的新确认完成后恢复。搭建窗口始终保持干净视图。

连续模式稳定布局每 0.8 秒检查、可用待确认候选每 0.12 秒补足三帧；暂停模式仅在按下暂停后采集并持续关闭投影，直到继续前的新确认完成才恢复。旧的最短消隐时间/帧数下限及周期性消隐 API 已删除；这仍不代表硬件同步已完成。几何深度只用于生成相机画面，不向 `FixedGridRecognizer` 提供积木占用、颜色或 ID。

## 文件与调用链

```text
确认布局 / 战斗状态
  → ProjectionSimulator + SimulationBattleOverlay（内容源）
  → VirtualProjectorContent Camera → RenderTexture（输出图像）
  → SimulationProjectedLight（固定透视、接收物与遮挡深度）
  → SurfaceLight.shader（表面反射光贡献）
  → TopCamera RGB / SimulationProjectionPreview
  → 原有 B 识别与确认流程
```

- `Assets/Scripts/Feedback/SimulationProjectedLight.cs`：输出图像、投影参数、每个观察场景的遮挡与绘制、相机和纹理释放。
- `Assets/Resources/ProjectedLight/ProjectorDepth.shader`、`SurfaceLight.shader`：投影仪深度、表面反射、近似衰减和模糊；放在 Resources 以确保 Player 不因未引用而剔除。
- `ProjectionSimulator.cs`、`SimulationBattleOverlay.cs`：复用原图案与动画，源 Renderer 默认不直接绘制。
- `Assets/Editor/SimulationProjectionPreview.cs`：复用输出与镜头参数，在自己的积木场景中计算遮挡；不再复制投影面片。
- `SimulationFeedbackSetup.cs`：安装新组件，并移除原安装流程对不存在的 `renderGameObjects` 投影字段的引用。

旧额外三维角色组件和五个样式材质已删除，场景及安装菜单也已移除引用。预览不再保留战斗模型/路线复制分支或空投影节点；战斗门面只提供实际使用的计数、刷新和入口标记命令。

## 验证与限制

运行：

```powershell
pwsh -File Tests/RunSimulationDualWindowChecks.ps1 -CheckClass PhysicalProjectorChecks
pwsh -File Tests/RunSimulationDualWindowChecks.ps1 -CheckClass LiveProjectionRefreshChecks
pwsh -File Tests/RunSimulationDualWindowChecks.ps1 -CheckClass PauseRecognitionChecks
pwsh -File Tests/RunLogicChecks.ps1
```

图像检查覆盖黑场、残余光、颜色反射、固定幻灯片的位移/高度偏移、校正内容在中心和四角的实际光斑位置（0/1/2 高度、正投/斜投/运行时改变 FOV）、斜向遮挡、相机层过滤不能消除表面光、无投影搭建视图、预览不再贴面片、观察视角独立、消隐恢复、停用清理，以及真实 RGB 的干净识别和动态战斗输出。高度 fixture 由测试显式给定，不读取真值替代识别。验证 PNG/JSON 生成在本机 `.scratch/dual-window/`，不提交大体积截图。

最新复测命令、结果与覆盖范围统一见 [当前验证摘要](verification/current-simulation.md)。历史几何修复过程见 [贴合讨论](history/projection-alignment-analysis-2026-11-06.md)，初期实现记录见 [投影研究](research/physical-projector-github.md)。

这是直接光照的可控近似：尚未进行实机辐射校准，没有镜头畸变、真实失焦、多次反射、设备响应曲线或曝光积分。单投影仪、固定方形输出和单层识别仍是当前边界。

## 当前两种模式的操作

| 模式 | 投影 | 识别触发 | 继续操作 |
| --- | --- | --- | --- |
| 连续识别（默认） | 一直显示 | 自动采集实际带投影 RGB，并进行光度恢复 | 普通游戏暂停不关闭投影，识别仍可进行 |
| 暂停识别 | 正常显示时不自动采样；暂停采集期间保持关闭 | 按战斗面板“暂停识别/暂停”，在暂停期间采集独立干净 RGB 并确认摆放变化 | 按“继续”后等待新帧确认，随后恢复投影和战斗 |

准备阶段同样可用暂停按钮采集；直接开始时自动取得新确认再开战。模式切换保留已确认布局及战斗暂停状态。取消、重试、停用自动识别/组件或采集异常会恢复投影；继续等待超时会保留战斗暂停并恢复显示，便于修复后重试。收起识别面板不等于停用自动识别。只有合格的独立帧可推进确认，不复制原工程的同图多次确认循环。

当前没有“周期性消隐识别”或“合成图识别”产品入口。旧的 ProjectionBlankingSyncChecks/ProjectionCadenceChecks 已由 PauseRecognitionChecks 替换；旧检查结果仅保留在研究和历史记录中。合成器仅是纯数学测试 fixture。

当前验收结果统一见 [验证摘要](verification/current-simulation.md)。暂停检查覆盖准备阶段、开战新确认、暂停中编辑、继续等待、模式切换、相机失败、禁用识别/相机/控制器、停止自动识别、超时和重试。
