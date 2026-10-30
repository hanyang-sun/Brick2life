# A/C 分支整合：GitHub 检索与适配

日期：2026-10-30。输入固定为 A `823fe2851055f5af2762231f00b80be4b1cb7e90`、C `56adb763384a6cb6c09f54661595e854e68d8163`，共同基础 `2710f9a78351ba6ec76dd3bf39ebb7955738305e`。

## 范围与检索

保留 A 的编辑器双窗口、只读轨道预览、模型层战斗锁及每局重建路线；整合 C 的角色纹样、变化圈淡出和战斗投影。保持 RGB 预测与真值评价分离，投影关闭采集必须同时隐藏角色及战斗投影。

本轮在 GitHub 重新检索 `PreviewRenderUtility`（Unity-Technologies/UnityCsReference）与 `GenerateModelPreview`（yasirkula/UnityRuntimePreviewGenerator），结果仍定位到以下固定版本。README、源码与许可证的实际阅读沿用本仓库 [双窗口检索记录](editor-dual-window-github.md)，投影渲染机制的比较沿用 [任务 C 检索记录](task-c-projection-github.md)。本轮另阅读了 C 分支的完整差异、ProjectionSimulator、SimulationBattleOverlay、SimulationDisplayController、BrickEditGate 及战斗控制器。

| 候选 | 已阅读版本与授权 | 本轮决定 |
| --- | --- | --- |
| [UnityCsReference](https://github.com/Unity-Technologies/UnityCsReference/tree/bf6028387a4fc2ea6d41aa7d8998e774655aedf6) | README、LICENSE.md、Editor/Mono/Inspector/PreviewRenderUtility.cs；Reference-Only | 继续使用公开 PreviewRenderUtility 接口，将新增投影几何镜像到独立预览场景，不复制参考源码。仅编辑器模块依赖它，运行时不增加包。 |
| [UnityRuntimePreviewGenerator](https://github.com/yasirkula/UnityRuntimePreviewGenerator/tree/87cbffc2d4457860199a5de1fae547315062f41a) | Plugins/RuntimePreviewGenerator/README.txt、RuntimePreviewGenerator.cs、LICENSE.txt；MIT | 缩略图生成会临时移动源对象或克隆模型，不适合持续双窗口和动态战斗预览；继续只参考资源隔离、状态恢复机制，不引入代码。 |
| [本仓库 C 分支](https://github.com/hanyang-sun/Brick2life/tree/56adb763384a6cb6c09f54661595e854e68d8163) | 同仓库成员的角色图案、投影计划、战斗覆盖层与场景接线 | 按用户授权合并自有代码。适配给 A 的双窗口与锁，避免并存两套显示控制及两套战斗锁。 |

两种外部候选均不提供本项目的确认布局及实验合同；A 已完成的预览模块更匹配当前 Unity 6000.3 编辑器需求。Android 不在双窗口范围内，运行时投影继续使用既有 Built-in 材质和无额外依赖的程序纹理，兼容性以实际测试为准。

## 适配决定

1. 主线保留 A 的两窗口，移除 C 单相机三模式工具条、舞台改层及 BrickEditGate，统一使用已有编辑焦点许可与 SimulationBoardSession 战斗锁。
2. 合入 C 的 ProjectionPlan、ProjectionSimulator、SimulationBattleOverlay 及纹样；新增投影几何进入专用投影层，同时进入两种展示模式，不混入仅组合模式可见的数字游戏对象。
3. 战斗控制器继续每局从已确认 LevelState 重建；等待开战只能依赖 B 新采集的确认结果，不读取原始棋盘占用、颜色或 ID 来判断识别是否正确。人工 fixture / 无自动视觉输入的独立演示保持显式确认驱动。
4. 投影采集门统一管理已注册的投影模块，临时关闭时保存并恢复各自状态，避免新战斗覆盖层残留在无投影 RGB 对照中。保持现有 F8 对照入口，不在本轮实施研究计划书中的算法实验。
5. 覆盖层读取战斗快照与射击事件；事件只消费一次，重试后清理，暂停不产生重复射击效果。展示窗口只镜像渲染状态，不执行游戏逻辑或改动采集源。

没有复制外部代码或新增第三方依赖，因此不新增第三方声明。验证包括纯 C#、实际 Unity 场景双窗口、战斗编辑锁及路径刷新，以及新增覆盖层进入预览、开关投影 RGB、等待确认的集成检查。仿真结果不解释为真实投影设备性能。
