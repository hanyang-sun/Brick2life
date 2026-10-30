# 任务 C 手册：关卡反馈与虚拟投影

更新：2026-10-30。适用工程：Brick2Life（Unity 6000.3.24f1，Built-in 渲染管线，默认场景 `Assets/Scenes/Brick2LifeSimulation.unity`）。A/C 整合以 [整合说明](a-c-integration.md) 及 [双窗口指南](editor-dual-window-guide.md) 为当前入口；历史验证记录保持各自测试时的条件。

## 1. 任务 C 在系统里的位置

```
仿真积木（GT，只给评价）          顶置相机 RGB
        │                              │
        │                     B：图像 → FrameObservation
        │                              │
        └──── 不进入 C ────►  BoardSyncSession ──► BoardUpdate（只消费这里）
                                                   │
                                    LevelFeedbackSession（Unity-free）
                                     ├─ LevelState：颜色 → 核心/城墙/防御塔
                                     └─ ProjectionPlan：格点 / 角色高亮 / 变更提示
                                                   │
                              SimulationLevelAdapter（引擎侧）
                                 ├─ SimulationLevelView → 游戏对象（Brick2LifeGame 层）
                                 └─ ProjectionSimulator → 投影面片（Brick2LifeProjection 层）
                                                   │
                                        顶置相机看到的图像（含投影、不含游戏对象）
```

C 不读 `SimulationBoardSession.Snapshot()`，不读 GT，也不创建观测。任务 C 的独立验收允许“用明确标注的人工目标或测试数据驱动投影”（`.scratch/task-a-simulation/spec.md` 第 2 阶段），本工程用 `SimulationFeedbackDemo` 提供这一入口，并在日志与手册中标注它不是识别结果。

## 2. 文件与职责

| 文件 | 职责 |
| --- | --- |
| `Assets/Scripts/Application/LevelSemantics.cs` | `GameRole`、`LevelRuleSet`（颜色→角色）、`LevelState`（逐格占用/颜色/角色 + 合法性 Issues） |
| `Assets/Scripts/Application/ProjectionPlan.cs` | `ProjectionMark`（格位矩形、种类、表面、颜色）、`ProjectionOptions`、`ProjectionPlanBuilder` |
| `Assets/Scripts/Application/LevelFeedbackSession.cs` | 消费 `BoardUpdate` → `FeedbackFrame`（关卡 + 投影计划 + 是否变化 + 原因） |
| `Assets/Scripts/Feedback/SimulationFeedbackBootstrap.cs` | 组合入口：建立 `BoardSyncSession`，交给适配器与演示组件（执行顺序 -50） |
| `Assets/Scripts/Feedback/SimulationLevelAdapter.cs` | 任务 C 适配器：`Apply(BoardUpdate)`、颜色规则、状态文字、角色→颜色反查 |
| `Assets/Scripts/Feedback/SimulationLevelView.cs` | 按 `LevelState` 生成/复用/销毁游戏对象，位置为格中心，统一放到 `Brick2LifeGame` 层 |
| `Assets/Scripts/Feedback/ProjectionSimulator.cs` | 按 `ProjectionPlan` 生成表面贴合面片，提供开/关、强度、对齐误差测量 |
| `Assets/Scripts/Feedback/SimulationFeedbackDemo.cs` | 人工目标演示（F5/F6/F7），明确标注非识别结果 |
| `Assets/Editor/SimulationFeedbackSetup.cs` | 菜单安装：层、材质、对象层级、组件接线、相机掩码；另有分层复核菜单 |
| `Tests/LevelSemanticsChecks.cs`、`Tests/ProjectionPlanChecks.cs`、`Tests/LevelFeedbackChecks.cs` | 纯 C# 断言（73 项） |

## 3. 语义规则

- `LevelRuleSet` 显式配置颜色→角色；未配置的颜色记 `UnmappedColor`，该格保留占用但 `Role=None`，不生成游戏对象也不高亮。
- 关卡合法性：必须恰好一个核心。缺核心记 `MissingCore`，多核心记 `MultipleCores`。非法关卡仍会发布 `LevelState`（保留感知结果），只是 `IsPlayable=false`，界面显示原因。
- 默认角色映射由安装菜单按调色板顺序写入：第 1 个颜色=核心，第 3 个=防御塔，其余=城墙。颜色语义（红=塔等）可按需在组件上改，规则不写死在代码里。

## 4. 投影内容

| 种类 | 覆盖范围 | 表面 | 默认颜色/强度 |
| --- | --- | --- | --- |
| `GridPoint` | 11×11 交点（每点 0.07 格见方，边界向棋盘内约束） | 相邻格有积木时投积木顶面，否则投桌面 | 冷白，alpha 0.16 |
| `RoleHighlight` | 所有占用且有角色的格（0.8 格见方） | 积木顶面 | 核心徽记 / 城墙砖纹 / 防御塔靶纹，alpha 0.32，核心有呼吸效果 |
| `ChangeHighlight` | 本次 `BoardDelta` 涉及的格（0.96 格见方） | 变化后仍有积木时投顶面，否则投桌面 | 新增绿 / 移除红 / 改色黄的轮廓圈，alpha 0.75，默认 1.6 秒淡出 |

- 位置：格 `(x, y)` 的中心是 `(x+0.5, y+0.5)`；表面高度由 `ProjectionSimulator` 决定（积木顶面 1.0、桌面 0.0，再加 0.006 偏移）。因此面片中心与格位中心的重合是构造保证；`ProjectionSimulator.MeasureAlignmentError(plan)` 可返回实际最大偏差（当前实现应为 0）。
- 面片是 `PrimitiveType.Quad`，绕 X 转 90° 落在水平面，材质为无光照 alpha 混合（安装菜单创建的 `ProjectionMark.mat` 用 `Sprites/Default`），`shadowCastingMode=Off`，且不生成 Collider，避免影响积木编辑的射线拾取。
- 投影强度 `intensity` 乘到 alpha 上；`SetVisible(false)` 关闭全部面片，用于“投影开/关”对照采集。
- 上限 `ProjectionOptions.MaxMarks`（默认 400）防止密集布局下标记爆炸；截断时 `ProjectionPlan.Truncated=true`。

### 4.1 投影与拍照时序

当前默认关闭投影取样作为无干扰对照；研究“投影干扰”时使用保留投影的采集条件。实现方式：

- `IProjectionGate`（`Assets/Scripts/Simulation/ProjectionCaptureGate.cs`）由角色投影与战斗覆盖层共同实现并注册；Simulation 程序集不依赖 Feedback。
- `SimulationCameraCapture.CaptureObservationImage()` 的默认行为：若 `captureWithoutProjection=true`（默认），通过 `ProjectionCaptureGate.Suppress()` 临时隐藏全部已注册投影 → 按 `projectionSettleFrames`（默认 2）追加 `sourceCamera.Render()` → 取像素 → `finally` 恢复各投影原有状态。采集失败也恢复，战斗覆盖层同步隐藏，不依赖下一帧 Update。
- `CaptureObservationImage(true)` 保留投影，用于干扰对照；返回的 `SimulationObservationImage.ProjectionVisible` 记录本次取样是否带投影。
- 运行时按 **F8** 实时切换“取样是否关闭投影”，日志会打印当前模式。
- 注意：这是仿真层面的时序（关掉面片后手动渲染即可稳定）；真实投影仪需要按设备响应时间等待，届时把 `projectionSettleFrames` 换成实际延时即可。

## 5. 场景分层

| 层 | 索引 | 内容 | 顶置相机 | 操作相机 |
| --- | --- | --- | --- | --- |
| UI | 5 | 工具栏、按钮、文字 | —（Canvas 为 Overlay，本就不进相机） | 排除 |
| `Brick2LifeGame` | 30 | 数字游戏对象（核心/城墙/塔） | **排除** | 包含 |
| `Brick2LifeProjection` | 31 | 投影面片 | **包含** | 包含 |

场景里 `TopCamera` 的掩码已从 `4294967295` 改为 `3221225471`（全部层去掉 30），`WorkspaceCamera` 保持 `4294967263`（全部层去掉 5）。这样：

- 虚拟建筑不会被拍进 RGB（否则 B 会把虚拟塔当成积木）；
- 投影会进入 RGB（这正是要研究的“投影干扰”来源）；
- 关掉投影层对象即可得到同布局的干净对照图。

菜单 `Brick2Life/Simulation/Verify feedback layering` 会打印三项布尔值与顶置相机掩码，用于运行时快速核对。

## 6. 安装与演示（需要 Unity 6000.3.24f1）

1. 用 6000.3.24f1 打开工程，打开 `Brick2LifeSimulation` 场景，确认已退出 Play Mode。
2. 菜单 `Brick2Life/Simulation/Install feedback`。它会：
   - 确保 30/31 层名存在（占用时直接报错，不覆盖别人的层）；
   - 在 `Assets/Materials/Simulation/` 创建 `Game_Core.mat`、`Game_Wall.mat`、`Game_Tower.mat`、`ProjectionMark.mat`（已存在则复用）；
   - 创建 `SimulationFeedback`（含 `Level`、`Projection` 子节点）并挂上 View/Projection/Adapter/Demo/Bootstrap；
   - 按调色板写入颜色→角色规则；
   - 调整两台相机的掩码并保存场景。
3. 进入 Play，按 `F5` 依次提交三个人工目标，按 `F6` 提交空布局，按 `F7` 开/关投影。Console 会打印 `人工目标 …（非识别结果）`、关卡计数、核心/角色问题。
4. 建议的核对顺序：`F5` 两次（第二次应看到变更高亮）→ `F6`（应提示缺核心，游戏对象被清空）→ `F5` 恢复 → `F7` 关闭投影后保存一张 RGB 对照（`保存 RGB + 标注` 按钮），再开投影保存一张，两张应只差投影内容。

## 7. 接入任务 B

B 接通后：

1. 禁用 `SimulationFeedbackDemo`（Inspector 取消勾选组件），避免人工目标覆盖真实确认。
2. B 每次识别后调用 `SimulationFeedbackBootstrap.Submit(update)`（内部转 `SimulationLevelAdapter.Apply`），或直接持有 Adapter 调 `Apply`。
3. C 的 `Rejected`/`Stale` 分支已经按 B 的失败语义实现：保留上一版关卡与投影，不刷新视图。

## 8. 已知限制

- 投影是“贴在可见表面上的面片 + alpha 混合”，不是光学投影仿真：没有镜头畸变、亮度衰减、梯形校正、投影阴影；因此它只能支持“投影颜色干扰识别”这类实验，不能宣称真实投影设备的表现。升级路径是官方 Built-in `Projector` 组件（正交、Ignore Layers）+ 投影贴图，只替换 `ProjectionSimulator` 的渲染层，Application 层接口不变。
- 单层 10×10、等大积木；多层、遮挡、隐藏层不在本轮范围。
- 语义关卡只做“唯一核心”等合法性判定；寻路、波次和战斗已由当前 `Brick2Life.Battle` 新管线承接，归档代码不进入编译。
- 目标版本验收已于 2026-10-27 在 `6000.3.24f1` 上完成（第 6 节四项全部通过：编译零错误、安装菜单与 Play、对齐误差 0、投影进入相机且游戏对象零影响、Windows x64 构建成功）；2022.3 适配副本的验证见第 10 节。

## 9. 验证命令

```powershell
# 纯 C# 断言 788 项（包含投影计划 29 项、原战斗 104 项、实时改图 188 项）
dotnet run --project Tests/Brick2Life.Checks.csproj
```

Unity 侧（有 6000.3.24f1 的机器）按第 6 节执行，并记录：面片中心与格位中心偏差、投影开/关的顶置图像差异、顶置相机是否拍到游戏对象。

## 9.1 战斗（新管线）

战斗逻辑在 `Brick2Life.Battle`（无 Unity 依赖），场景侧是 `SimulationBattleController` + `SimulationBattleView` + `SimulationBattlePanel`，安装菜单会一并装好（`Battle` 与 `BattlePanelRoot`）。

- **入口**：点面板里的“点棋盘选入口”后进入选择模式——参数面板自动收起（左上角只剩“战斗面板 ▾”开关），识别面板临时隐藏，棋盘完整可见，屏幕上方提示“点亮中：点棋盘空格选入口”。在 **Game 视图** 的棋盘空格上逐点选择，每选中一个会出现一根**黄色入口柱**，状态栏同时给出坐标；选完点提示条上的“结束选择”，面板恢复。`清空入口` 清除全部。选择期间积木编辑被临时关闭（否则同一次左键会加积木，这是之前选不中入口的根因），点在积木、面板或棋盘外会给出对应提示。每多一个入口就多一条 A* 通路，敌人按入口轮转（跨波继续）。
- **参数**：波数（1–20）、每波数量（1–50，总数 ≤400）、敌人生命（1–10000）、速度（0.1–20 格/秒）、核心伤害（1–1000）、出怪间隔（0.1–10 秒）、核心血量、塔伤害、塔攻击间隔；越界参数会被 `BattleValidation` 拒绝并在状态栏说明。
- **操作**：`开始` 从第 1 波开始；一波清空后 `下一波`；`暂停/继续` 冻结时钟；`重试` 结束当前局面并回到可选入口、可改参数的状态。
- **实时布局**：战斗中可以放置、删除和移动积木。B 稳定确认后，地图、塔与路线更新；敌人从当前位置接续，本局波次、血量与计时保留。无核心、堵路或敌人脚下出现障碍时等待修正，恢复后继续。用户暂停与布局等待分别管理。
- **规则**：四方向 A*；墙与塔阻挡；塔每 `攻击间隔` 对射程内最近敌人开火；敌人到达核心扣 `核心伤害`；核心 0 血失败，最后一波清空胜利。

## 10. 在 Unity 2022.3 上试运行（非目标版本）

如果没有 6.3、又想在真实编辑器里验证任务 C，可以做一个**适配副本**（不要改仓库本身）：

1. 复制 `Assets`、`Packages`、`ProjectSettings` 到新目录，例如 `E:\Brick2Life-2022`。
2. `ProjectSettings/ProjectVersion.txt` 改成 `2022.3.62f3c1`。
3. `Packages/manifest.json`：`com.unity.ugui` 改为 `1.0.0`；删除 `com.unity.modules.accessibility`、`com.unity.modules.adaptiveperformance`、`com.unity.modules.vectorgraphics`（2022.3 没有这些模块）；删掉 `packages-lock.json`。
4. 用 2022.3 打开该副本，打开 `Assets/Scenes/Brick2LifeSimulation.unity`，运行菜单 `Brick2Life/Simulation/Install feedback`，进 Play 按 F5/F6/F7。

2026-10-25 在本机按此流程得到的实测结果（详见验证记录）：关卡对象 3 个、投影面片 134 个、对齐误差 0、顶置相机看不到游戏对象但能看到投影（开/关图像差 25136 像素）、隐藏游戏对象后顶置图像 0 像素变化。

限制：`2022.3` 没有 uGUI 2.0.0，也没有 Unity 6 的部分序列化/渲染行为，所以副本只用于“逻辑 + 接线 + 分层 + 投影可见性”的验证，不能替代 6.3 的编译、运行时与构建验收；副本也不要拿去覆盖仓库。

## 9.2 展示模式与投影效果

- Unity 编辑器 Play 模式同时打开“原始积木布局”和“投影展示”两个独立窗口。原始窗口编辑与战斗操作；展示窗口只读，提供“积木＋投影／仅投影”切换、旋转、缩放和视角复位；纯投影保留暗色棋盘轮廓。切换展示不更改源对象或识别相机。C 原分支 F1/F2/F3 的单窗口工具条不与双窗口并存，也不再引入舞台改层或第二套编辑锁。
- 战斗投影覆盖层：路径流动箭头、每塔范围环、开火闪光、命中涟漪、核心冲击环（掉血时）、城墙局部波纹（敌人贴墙经过时）、塔朝向箭头（指向最近敌人）。
- 开战与实时编辑：自动识别开启时，点“开始”后等待 B 新 RGB 的确认；战斗中继续确认新布局并更新本局，不读取原始棋盘判断识别对错。积木在暂停和波次间隔中也可编辑，出生点仍固定。重试可取消开战等待，超时或会话变更提示重新开始。人工目标与独立 fixture 在无自动视觉输入时直接使用已确认观测。详见 [实时交互指南](live-battle-editing-guide.md)。
