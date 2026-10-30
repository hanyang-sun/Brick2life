# 编辑器双窗口布局与投影预览

日期：2026-10-29。用户要求仅支持 Unity 6000.3.24f1 编辑器 Play 模式：两个独立窗口同时可见；原始窗口编辑和控制战斗；展示窗口只读，有“积木＋投影”和“仅投影”两种模式，两者都允许旋转缩放，纯投影保留暗色棋盘轮廓。前两项路径刷新与战斗锁继续有效。

## 搜索与实际阅读

GitHub 搜索关键词 `PreviewRenderUtility`（Unity-Technologies/UnityCsReference）和 `RuntimePreviewGenerator`（yasirkula/UnityRuntimePreviewGenerator）。源码与许可证先于本次实现阅读。

| 候选 | 已阅读与授权 | 比较及决定 |
| --- | --- | --- |
| [UnityCsReference](https://github.com/Unity-Technologies/UnityCsReference/tree/bf6028387a4fc2ea6d41aa7d8998e774655aedf6) | 根 README、LICENSE.md（Reference-Only）；`Editor/Mono/Inspector/PreviewRenderUtility.cs` 中预览场景/相机创建、AddGameObject/AddSingleGO、Render 的恢复路径、Cleanup/Dispose；camera、lights 的公开接口。当前参考源码 README 标为 7000，因此实际兼容性以本机 6000.3.24f1 验证为准。 | 原生 EditorWindow 加 PreviewRenderUtility 提供独立预览场景与相机，适合本次仅编辑器需求。只调用公开接口，不复制参考源码。原始窗口用 Unity 自带 GameView 实例，类型定位仅放在编辑器模块中。 |
| [UnityRuntimePreviewGenerator](https://github.com/yasirkula/UnityRuntimePreviewGenerator/tree/87cbffc2d4457860199a5de1fae547315062f41a) | `Plugins/RuntimePreviewGenerator/README.txt`、`RuntimePreviewGenerator.cs` 的 CameraSetup、GenerateModelPreviewInternal、独立 RenderTexture 与 finally 恢复位置/图层/相机的流程；根 LICENSE.txt（MIT，2017 Süleyman Yasir KULA）。 | 可生成模型缩略图，支持克隆；非克隆流程会临时移动对象、换图层，且没有持续双窗口、观察输入和 B 确认状态接入。只参考独立相机、纹理与清理机制，不引入源码、包或全局运行时图层切换。 |

## 采用与适配

1. 编辑器模块拥有双窗口的打开/关闭、输入焦点和原始相机掩码恢复；只改变 WorkspaceCamera 对游戏/投影层的可见性，保持 TopCamera、RGB 和 GT 采集配置。
2. 只读展示由 B 确认的 `LevelState` 重建积木外观，颜色读取公开调色板。读取已有投影面片的几何与属性块；战斗观察只镜像 BattleView 的可见几何。不读取真值占用或绕过确认状态。
3. 预览几何全部处于 PreviewRenderUtility 的独立场景，无积木脚本和 Collider。独立 RenderTexture 支持模式切换、轨道相机与缩放，不修改源对象激活状态。
4. 小型 `SimulationEditAccess` 模块提供编辑焦点许可；编辑器窗口只作为焦点提供者。公共编辑方法、鼠标拾取与出生点操作都检查许可，战斗锁仍独立生效。未打开双窗口时默认许可，保留 Player 与现有测试行为。
5. 原始窗口复用现有 UI；展示只提供模式切换、视角复位和观察输入。进入 Play 自动打开，退出/重载时释放窗口、预览场景、材质与纹理，并恢复相机和焦点许可；批处理不自动打开窗口。

## 验收

- 两窗口可独立存在，只有原始窗口接受积木与出生点编辑；展示拖动和滚轮只改变预览相机。
- 显示模式不改变源积木、投影状态与顶置相机；未确认的原始改动不提前进入展示。
- 纯投影无积木模型但有暗色底板轮廓，两种模式均可旋转缩放。
- 关闭/停止 Play 后恢复编辑许可和 WorkspaceCamera；保留战斗锁、路径刷新与基础测试。
- 本次不增加 Player 多窗口或真实投影仪输出能力，不复制第三方代码。

## 实施验证

- Unity 6000.3.24f1 独立工程、真实仿真场景：35 项断言通过，涵盖两窗口创建/关闭、真实焦点切换、确认布局与等待状态、两种显示模式、旋转缩放、编辑隔离、焦点与战斗锁联动、相机配置恢复。
- 同布局顶置 RGB 在预览渲染前后对照，超过 1 通道级差的像素为 0；原始积木版本、数量、投影可见性与顶置相机配置保持一致。
- 170 项战斗编辑锁 Play 检查、130 项新局路径刷新检查和原有 596 项纯 C# 检查通过。
- 本机记录：`.scratch/dual-window/run-20261029-172644-289/verification.json`；显示示例：同目录 `combined.png`、`projection-only.png`。示例使用明确标注的确认观测 fixture，不作为 RGB 识别精度证明。
- 用户操作见 `docs/editor-dual-window-guide.md`。
