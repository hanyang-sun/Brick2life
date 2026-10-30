# 战斗期间布局编辑锁：检索与适配

日期：2026-10-29。范围：开始战斗后锁定原始积木与出生点；暂停、等待下一波保持锁定；胜负结束或重试后解锁。保留前一项“新局重新规划路线”的修复。

依据：用户确认的第 2 项需求、`docs/architecture.md`、现有 `SimulationBattleController`、`SimulationBrickEditor` 与 `SimulationBoardSession`。本次不更换输入系统，不调整投影或界面布局。

## 检索与实际阅读

GitHub 搜索：在 Unity-Technologies/uGUI 检索 `Selectable interactable`，在 Brackeys/Tower-Defense-Tutorial 检索 `CanBuild`；检查仓库目录确认 README、源码和许可证的实际位置。

| 候选 | 阅读范围与许可 | 适用机制、差异与决定 |
| --- | --- | --- |
| [Unity-Technologies/uGUI](https://github.com/Unity-Technologies/uGUI/tree/8dba21dfe95d65d1454e323847c73664b91b4125) | `com.unity.ugui/README.md`、`LICENSE.md`（Unity Companion License）、`Selectable.cs` 的 `OnCanvasGroupChanged`、`ParentGroupAllowsInteraction`、`IsInteractable`；`Button.cs` 的 `Press` | 复用工程已安装的 uGUI：`CanvasGroup.interactable` 禁用整组编辑控件，出生点按钮使用 `Button.interactable`。按钮正常点击会检查交互状态，但直接调用 UnityEvent 或目标方法仍可绕过，所以必须在写入入口再次检查。不复制包源码，不新增依赖。现有 uGUI 2.0.0 的兼容性以本机 Unity 6.3 测试为准。 |
| [Brackeys/Tower-Defense-Tutorial](https://github.com/Brackeys/Tower-Defense-Tutorial/tree/166b442b516ca5f2dd09838b355ba0c96438f598) | 根目录 README、`LICENSE.md`（Unlicense / public domain）；`Node.cs`、`BuildManager.cs`、`GameManager.cs` | 借鉴在行为入口检查许可、集中管理游戏阶段的机制。该教程的 `CanBuild` 只表示选中了塔，并没有实现本项目的战斗期间禁建；`GameIsOver` 也不等于暂停/波次间隔。因此不直接移植，只把本项目已有 `Running` 作为战斗锁来源。源码不复制。 |

## 实施方式

1. `SimulationBoardSession` 保存编辑锁并通知观察者；`TryAdd`、`TryMove`、`TryDelete` 在锁定时返回失败，`ReplaceLayout` 拒绝修改。读取布局、拍摄和识别继续运行。
2. `SimulationBrickEditor` 订阅锁状态，取消未完成拖拽，禁用已有 BrickTools 控件组，并在公共按钮方法和鼠标处理入口检查锁。仅停用 MonoBehaviour 不足以拦住按钮回调。
3. `SimulationBattleController` 根据本局是否仍在进行持有/释放棋盘锁；暂停和等待下一波仍锁定。手动步进与正常 Update 使用同一状态发布路径。控制器销毁时释放其持有的锁。
4. 出生点选择属性、增加/清空方法和对应按钮都服从同一战斗阶段限制。
5. 回归测试在隔离 Unity 工程中运行，检查按钮事件、直接方法调用、拖拽取消、暂停/下一波、胜负/重试解锁、失败开局和新局重新加锁。原有路径刷新测试保持通过。

## 授权与成本

本次只有机制参考和已有公开 API 调用，不复制第三方实现；无新增包、平台依赖或网络运行要求。无需增加第三方代码声明。Android、离线运行和既有程序集依赖不因本次设计改变。

## 验证结果

- Unity 6000.3.24f1 隔离 Play 模式：170 项断言通过，验证编辑按钮与出生点按钮禁用、直接 UnityEvent 回调和棋盘写入受限、拖拽取消、F5/F6 禁止改布局、暂停和波次间隔保持锁定、胜负与重试后恢复、失败开局不锁定、控制器销毁释放锁。
- 原路径刷新回归：130 项断言通过；新一局继续采用最新确认布局与当前出生点，同一局保持固定。
- `dotnet run --project Tests/Brick2Life.Checks.csproj --no-restore`：原有 596 项断言通过。
- 运行编辑锁检查：`pwsh -File Tests/RunBattleControllerChecks.ps1 -CheckClass BattleEditingLockChecks`。默认不带 `-CheckClass` 则运行路径刷新检查。
- 本机记录在 `.scratch/battle-controller/run-20261029-142440-676/verification-BattleEditingLockChecks.json` 和 `verification-BattleControllerChecks.json`。检查使用独立工程，不关闭用户打开的编辑器。
