# 原始布局与投影展示双窗口

适用：Unity 6000.3.24f1 编辑器 Play 模式，默认场景 `Assets/Scenes/Brick2LifeSimulation.unity`。本次新增编辑器窗口，不增加 Windows Player 多窗口。

## 使用

1. 等待脚本编译结束；若修改时处于 Play，先退出再重新进入。
2. Play 时自动打开“原始积木布局”和“投影展示”两个浮动窗口。也可在菜单 `Brick2Life/Windows/Open layout and projection` 手动打开。
3. 原始窗口保留积木编辑、识别与战斗操作。必须点击该窗口取得焦点后才能编辑积木或出生点；战斗、暂停、波次间隔仍受战斗锁约束。
4. 展示窗口仅用于观看。顶部切换“积木＋投影”或“仅投影”，两种模式都支持左键/右键拖动旋转、滚轮缩放和“复位视角”。纯投影保留暗色底板与网格轮廓。
5. 两窗口可以分别移动和调整大小。退出 Play、关闭窗口组或使用 `Brick2Life/Windows/Close layout and projection` 后释放预览并恢复原始相机与输入许可。

## 显示来源

- 展示的积木由 B 已确认的 `LevelState` 重建；未确认时显示等待状态。展示不读取 JSON 真值占用，也不直接镜像原始棋盘的积木实例。
- 积木颜色使用约定的公开调色板；投影镜像现有面片几何、颜色与强度。组合模式还显示已有战斗观察几何，纯投影模式隐藏积木与战斗模型。
- 展示使用独立的预览场景、相机与 RenderTexture，隐藏模型及观察操作不修改源对象。原始 WorkspaceCamera 排除游戏/投影层；实验 TopCamera 的配置和取样流程保持原状。
- 编辑焦点限制只拦截用户编辑入口，不把窗口焦点当作模型锁；拍照、识别与后台数据生成仍可读取和使用模型。战斗锁独立控制模型写入。
- 本次沿用现有投影内容；新的角色纹样和战斗投影特效属于后续视觉工作。

## 验证入口

`pwsh -File Tests/RunSimulationDualWindowChecks.ps1` 在隔离工程运行真实场景，验证确认状态显示、两种模式、轨道相机、焦点限制、双窗口创建/关闭、战斗锁与原始 RGB 不变，并导出 `combined.png` 和 `projection-only.png`。

机制与候选比较见 [检索记录](research/editor-dual-window-github.md)。
