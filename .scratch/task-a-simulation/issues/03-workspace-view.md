Status: ready-for-human

# 正交三维工作台与顶置小预览

用户于 2026-10-16 确认操作视图采用正交斜俯视。先配置固定视图，后续再分步加入相机控制与积木编辑。

## 本步内容

- 新增 WorkspaceCamera 显示三维工作台，能看到积木顶面、侧面和完整棋盘。
- 现有顶置 RGB 缩到右侧，保持同一 512×512 纹理和 1:1 显示。
- 明确相机引用、MainCamera 标签和唯一启用的 AudioListener。
- 当前主编辑器实际运行，检查显示、相机独立性和常用横向窗口尺寸。
- 只新增随窗口比例调整操作相机取景的运行脚本，不接入交互旋转、缩放、鼠标编辑、识别、投影或多层。

## 依据

编码前比较：`docs/research/simulation-workspace-view-github.md`。

## Comments

- 使用临时 Editor 辅助工具先读取当前内存状态，再配置；保留已有未提交修改及 `.meta`。
- 新场景对象、相机参数、UI 显示链路与验证方式在交付说明中逐项解释。

## 实施结果

- 已新增正交斜俯 WorkspaceCamera 和随窗口比例取景的 SimulationWorkspaceCamera，主区域占左侧 72%；三色积木能显示顶面和侧面。
- 右侧保留原 TopCameraRGB 和 1:1 RawImage，增加中文说明；顶部固定留白避免宽屏下文字与图像重叠。
- MainCamera 标签与唯一 AudioListener 归 WorkspaceCamera，TopCamera 的拍摄参数不变。
- 当前主编辑器实际 Play：571×721、1024×768、1280×720 三种尺寸通过；改变操作相机姿态前后，实验图像 SHA-256 一致。已恢复原 Game 尺寸及原 Play 状态。
- 场景备份、截图、像素计数与验证限制见 `../workspace-view/README.md`；教学说明见 `docs/task-a-workspace-view-guide.md`。等待用户查看本步配置与学习后进入下一小步。
