# 任务 C：虚拟投影与游戏反馈

依据：用户 2026-10-25 指定完成分工任务 C（`E:\BrickDefense\fengong.docx` 第三节；`Brick2Life_Research_Guide.docx` 11.1）；实施依据为本仓库 `README.md`、`docs/architecture.md`、`docs/brick2life-development-plan.md` 与 `.scratch/task-a-simulation/spec.md`。

编码前检索记录：`docs/research/task-c-projection-github.md`。

## 已确定

- C 只消费预测事件：输入是 `Brick2Life.Application.BoardUpdate`（`BoardSyncSession.Confirm/Fail` 的返回值）。不读 `SimulationBoardSession.Snapshot()`、不读 GT、不创建观测。
- 语义角色沿用旧原型词汇：`Core`（核心）、`Wall`（城墙）、`Tower`（防御塔）；颜色到角色由 `LevelRuleSet` 显式配置，默认红=塔、绿=墙、蓝=核心、黄=核心（与场景四色材质对应，可改）。
- 关卡合法性只做第一阶段需要的判定：必须恰好一个核心；出现未配置角色的颜色、或没有核心时，保留感知状态但推迟关卡提交（`IsPlayable=false` 并给出原因）。
- 投影内容三类：格点（桌面 11×11 交点）、角色高亮（占用格顶面）、变更提示（本次 `BoardDelta` 涉及的格）。全部为 3D 面片，贴在可见表面之上并参与相机渲染；不是 UI 覆盖。
- 投影渲染本轮采用“逐格表面贴合面片 + alpha 混合”，不引入官方 `Projector` 组件与自定义 shader；升级路径与代价见研究记录。投影参数（强度、格点开关、高亮开关、变更提示开关）可配。
- 场景分层：新增用户层 `Brick2LifeGame`（30）与 `Brick2LifeProjection`（31）。顶置相机排除 `Brick2LifeGame`、包含 `Brick2LifeProjection`；操作相机包含两者、继续排除 UI 层（5）。
- 独立投影仿真按 `.scratch/task-a-simulation/spec.md` 第 2 阶段执行：由人工标注的目标布局驱动，界面上明确标注“人工目标 / 非识别结果”；该演示不作为识别已接通的证据。
- 接入 B 后：`B` 的 `BoardUpdate` 调 `SimulationLevelAdapter.Apply`，关闭人工目标驱动组件即可，不改 C 的接口。

## 验收目标

在 Unity 6.3 中运行 `Brick2LifeSimulation`：提交一次基线确认后出现与角色对应的游戏对象；再次提交含增删改的确认后，游戏对象与投影同步变化；投影面片与格位中心重合、位于对应表面之上；顶置相机图像包含投影、不包含游戏对象；关闭投影层可得到同布局的对照图像。纯 C# 层能对上述规则逐条断言。

本轮无法在 Unity 中执行的项（本机只有 2022.3.62f3c1）：面片与格位对齐、顶置相机分层效果、投影开/关对照图像、构建产物。实施时以编译基线与纯 C# 检查为准，并在手册与验证记录中列明。

## 分阶段交付

1. Application 纯逻辑：`LevelSemantics`、`ProjectionPlan`、`LevelFeedbackSession` + 检查（可执行）。
2. Simulation 视图：`SimulationLevelView`（游戏对象）、`ProjectionSimulator`（面片）、`SimulationLevelAdapter`（消费 `BoardUpdate`）。
3. 场景装配与演示：Editor 安装菜单（层、材质、组件接线、相机掩码）、人工目标驱动组件。
4. 文档：`docs/task-c-feedback-guide.md`、`docs/architecture.md` 更新、验证记录。

## 当前进度

- 2026-10-25：编码前检索（`docs/research/task-c-projection-github.md`）与规格完成。
- 2026-10-25：Application 纯逻辑完成并通过 73 项新断言（关卡语义 22、投影计划 25、反馈会话 26）；全量检查 463 项通过。
- 2026-10-25：`Brick2Life.Feedback` 程序集（Bootstrap/Adapter/View/Projection/Demo）与 Editor 安装菜单完成；层 30/31 写入 `TagManager`，场景顶置相机掩码排除层 30；用 Unity 2022.3 程序集完成逐程序集编译基线，全部通过。
- 待做（需要 Unity 6000.3.24f1）：在目标编辑器运行安装菜单、进入 Play 核对对齐/分层/投影开关对照、Windows 构建。步骤见 `docs/task-c-feedback-guide.md` 第 6 节，结果记录见 `verification/README.md`。

