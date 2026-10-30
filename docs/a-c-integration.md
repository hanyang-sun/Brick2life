# A/C 整合说明

日期：2026-10-30。整合基础为 A `823fe28` 与 C `56adb76`，保留双方历史，以 A 的双窗口作为使用入口。目标版本为 Unity 6000.3.24f1，编辑器 Play 模式。

## 整合后的行为

- 原始积木布局与投影展示是两个同时可见的独立窗口；只有点回原始窗口才允许编辑。展示可旋转缩放，并切换积木＋投影或仅投影；纯投影保留暗色棋盘轮廓。
- 复用 C 的核心徽记与呼吸、塔靶纹与瞄准朝向、墙砖纹，以及路径箭头、每塔射程圈、开火闪光、命中涟漪、核心冲击环和邻墙波纹。变化提示用轮廓圈并淡出，移除标记落在桌面。
- 每局使用当前确认布局与入口重新规划，战斗中锁定布局与入口，暂停和波次间隔也不能修改。胜负结束或重试后解锁。
- 自动识别开启时，点开始先等待 B 新采集的 RGB 完成确认，再开战；重试可取消等待，超过 8 秒或确认会话改变则取消并提示。没有自动视觉输入的独立人工目标演示继续使用明确提交的确认布局。
- F8 独立控制识别是否保留投影，角色投影与战斗覆盖层统一受控。展示模式不决定识别输入。

进入默认场景 Play 后自动打开窗口；也可使用 `Brick2Life/Windows/Open layout and projection`。旧工程重新安装反馈使用 `Brick2Life/Simulation/Install feedback and save scene`，退出 Play 后执行。F7 保留总投影开关。

## 重叠实现的处理

采用 A 的 SimulationBoardSession 模型写入锁与 SimulationEditAccess 焦点许可，移除 C 单窗口 F1/F2/F3 工具条、舞台改层和 BrickEditGate，避免显示与锁定状态相互覆盖。

C 的等待开战原先通过读取仿真棋盘占用和颜色核对识别结果；整合后改为等待 B 的新采集确认序列，不把真值用于业务正确性判断。已有真值评分仍在预测之后独立计算。

新战斗覆盖层拥有独立投影子节点，预览同时镜像角色与战斗面片，并从数字游戏对象镜像中排除投影层，防止重复显示。两个投影模块统一注册到采集门；临时隐藏不改变模型或游戏时钟，异常也恢复显示。

同时修正了射程圈计数时重复使用去重集合导致没有可见圆环的问题，将每塔射程圈、瞄准箭头和核心冲击环定位到原始格位中心；射击效果按战斗推进批次消费一次，暂停不重复生成，重试清理旧效果。

## 主要调用链与文件

| 调用链 | 主要文件 |
| --- | --- |
| 点击开始 → B 新采集确认 → 当前 LevelState → 每局 Build → BattleSimulation | `SimulationBattleController.cs`、`SimulationVisionMonitor.cs` |
| 已确认布局／差异 → ProjectionPlan → 角色图案与变化圈 | `ProjectionPlan.cs`、`ProjectionSimulator.cs` |
| 战斗状态／射击批次 → 动态投影面片 | `SimulationBattleOverlay.cs`、`SimulationFeedbackSetup.cs`、默认场景 |
| 角色投影＋战斗投影 → 独立只读预览 | `SimulationProjectionPreview.cs`，沿用 A 双窗口模块 |
| RGB 取样 → 全部投影临时隐藏／恢复 | `ProjectionCaptureGate.cs`、`SimulationCameraCapture.cs` |

GitHub 候选与复用决定见 [检索记录](research/a-c-integration-github.md)。本次整合自有分支代码，无新增第三方包。

## 验证结果

- 纯 C# 检查：600 项通过，包含投影计划 29 项。
- 实际 Unity 战斗路线检查：130 项通过；编辑锁 Play 检查：170 项通过。
- 实际 Unity 场景集成检查：155 项通过，包含保存场景接线、安装菜单两次执行无重复、双窗口与焦点、新 RGB 确认后开战、角色图案、动态投影、暂停与重试、核心受伤位置、变化圈淡出及采集失败恢复。
- 预览操作前后源 RGB 变化为 0 像素；无投影采集与显式关闭全部投影的对照差异为 0 像素。保留投影时，顶置 RGB 的变化像素为 47,203（本轮三积木场景样例，通道差大于 1 计数）。该数字证明投影进入采集，不是识别准确率。
- 两种战斗展示截图已检查：`battle-combined.png`、`battle-projection-only.png`。场景检查先用明确标记的确认观测 fixture 验证显示来源，再用实际顶置 RGB 建立三积木布局验证采集及开战；fixture 不充当 RGB 识别证据。

本机证据位于 `.scratch/dual-window/run-20261029-172644-289/verification.json`，路线与锁证据位于 `.scratch/battle-controller/run-20261029-142440-676/verification-*.json`。隔离工程日志中仍有 Unity SearchDatabase 启动索引异常；未出现本轮脚本编译或模块异常，检查正常完成。

复测命令：

```powershell
dotnet run --project Tests/Brick2Life.Checks.csproj --no-restore
pwsh -File Tests/RunBattleControllerChecks.ps1
pwsh -File Tests/RunBattleControllerChecks.ps1 -CheckClass BattleEditingLockChecks
pwsh -File Tests/RunSimulationDualWindowChecks.ps1
```

本轮不等于真实投影设备验收，也不包含带投影准确率的成组实验。现有面片 alpha 混合、固定单层高度与格点相邻占用抬高仍是仿真限制；对齐数值不替代独立图像测量。
