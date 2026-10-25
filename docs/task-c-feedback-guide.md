# 任务 C 手册：关卡反馈与虚拟投影

更新：2026-10-25。适用工程：Brick2Life（Unity 6000.3.24f1，Built-in 渲染管线，默认场景 `Assets/Scenes/Brick2LifeSimulation.unity`）。

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
| `GridPoint` | 11×11 交点（每点 0.12 格见方） | 相邻格有积木时投积木顶面，否则投桌面 | 冷白，alpha 0.35 |
| `RoleHighlight` | 所有占用且有角色的格（0.86 格见方） | 积木顶面 | 核心蓝 / 城墙灰白 / 防御塔橙，alpha 0.55 |
| `ChangeHighlight` | 本次 `BoardDelta` 涉及的格（0.96 格见方） | 有积木时投积木顶面 | 新增绿 / 移除红 / 改色黄，alpha 0.8 |

- 位置：格 `(x, y)` 的中心是 `(x+0.5, y+0.5)`；表面高度由 `ProjectionSimulator` 决定（积木顶面 1.0、桌面 0.0，再加 0.006 偏移）。因此面片中心与格位中心的重合是构造保证；`ProjectionSimulator.MeasureAlignmentError(plan)` 可返回实际最大偏差（当前实现应为 0）。
- 面片是 `PrimitiveType.Quad`，绕 X 转 90° 落在水平面，材质为无光照 alpha 混合（安装菜单创建的 `ProjectionMark.mat` 用 `Sprites/Default`），`shadowCastingMode=Off`，且不生成 Collider，避免影响积木编辑的射线拾取。
- 投影强度 `intensity` 乘到 alpha 上；`SetVisible(false)` 关闭全部面片，用于“投影开/关”对照采集。
- 上限 `ProjectionOptions.MaxMarks`（默认 400）防止密集布局下标记爆炸；截断时 `ProjectionPlan.Truncated=true`。

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
- 关卡只做“唯一核心”判定，不包含寻路、波次、战斗；塔防运行时在归档的 `Archive/BrickDefense` 中，本任务不重建它。
- 本轮没有在 Unity 6.3 中实际运行（本机只有 2022.3.62f3c1）。已执行的是纯 C# 断言与 2022.3 程序集上的编译基线，运行时行为需按第 6 节核对。

## 9. 验证命令

```powershell
# 纯 C# 断言 463 项（含任务 C 的 73 项）
dotnet run --project Tests/Brick2Life.Checks.csproj
```

Unity 侧（有 6000.3.24f1 的机器）按第 6 节执行，并记录：面片中心与格位中心偏差、投影开/关的顶置图像差异、顶置相机是否拍到游戏对象。
