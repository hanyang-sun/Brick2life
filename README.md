# Brick2Life

Unity 中的积木交互与视觉反馈实验平台。当前实现 10×10 单层积木工作台、固定透视相机、可配置光照以及 RGB/真值配对采集。识别和投影由 B/C 后续接入，图像驱动的完整闭环尚未完成。

## 运行

使用 **Unity 6000.3.24f1**，Built-in 渲染管线、官方 uGUI 2.0.0。

```powershell
git clone --branch feat/task-a-unity-bricks-camera https://github.com/hanyang-sun/Brick2life.git Brick2Life
```

在 Unity Hub 添加工程，打开默认场景 `Assets/Scenes/Brick2LifeSimulation.unity`，点击 Play。侧栏支持选择颜色、添加、删除、拖拽移动、清空棋盘和保存 RGB + 标注。操作相机与实验相机彼此独立。

“光照”面板提供左右/双侧白光、亮度、左右强度比例、环境光倍数和阴影开关。默认双侧标准亮度、50:50、环境 1 倍。顶置实验相机使用 Perspective、FOV 50。

## 架构与分工

| 模块 | 当前职责 | 分工 |
| --- | --- | --- |
| Core | 固定棋盘定义、布局、不可变快照、观测与差异；无 Unity 依赖 | A 基础合同，B 差异 |
| Application | 确认会话、帧/标定校验、过期拒绝与失败保留；无 Unity 依赖 | 集成负责人 |
| Simulation | 独立棋盘会话、编辑界面、相机/光照采集和场景接线 | A |
| Vision | 待接入：RGB 与公开标定到固定格位观测 | B |
| Projection | 待接入：确认状态到暴露底板的反馈及坐标映射 | C |

Vision/Projection 尚无具体实现或空程序集。Core 的 `Contracts/` 是公共合同唯一来源。接入方式见 [架构说明](docs/architecture.md)，后续安排见 [开发方案](docs/brick2life-development-plan.md)。

## 采集数据

编辑器输出到根目录 `SimulationCaptures/`；Player 输出到 `Application.persistentDataPath/SimulationCaptures/`。每次写入同名 PNG/JSON，JSON 最后发布，包含全部 100 格、帧 ID、布局版本、棋盘四角、相机与光照配置。JSON 顶层 schema 3、相机 schema 1、光照 schema 2 保持兼容。

`gridX/gridY` 对应 Unity 世界 `X/Z`，高度为世界 `Y`；当前 `layer=0`、格长 1 仿真单位。图像原点在左上，数组按 `cells[y*width+x]` 排列。颜色与玩法角色独立。

GT 仅用于导出和评价。B 接收图像与约定公开配置，C 消费视觉确认状态。遮挡/未知不视为空格，相同布局不会产生新的确认版本。详见 [相机元数据](docs/task-a-camera-metadata-guide.md)、[光照控制](docs/task-a-lighting-guide.md)。采集数据及 Unity 验证缓存保存在本机。

## 检查与构建

纯 C# 检查需要 .NET 10 SDK，默认包含布局、快照及确认会话：

```powershell
dotnet run --project Tests/Brick2Life.Checks.csproj
```

Unity 检查在独立临时工程运行，可通过 `-UnityPath` 指定编辑器：

```powershell
pwsh -File Tests/RunSimulationCameraMetadataChecks.ps1
pwsh -File Tests/RunSimulationLightingChecks.ps1
```

Windows x64 构建可用菜单 `Brick2Life/Build/Windows x64`，或关闭当前工程编辑器后执行：

```powershell
pwsh -File Tests/BuildWindows.ps1
```

构建入口检查程序集和缺失脚本，输出 `Builds/Brick2Life/Brick2Life.exe`。本次验证结果见架构说明。

## 历史与授权

旧源码、场景、测试和专属算法已归档到 [Archive/BrickDefense](Archive/BrickDefense/INDEX.md)，退出 Unity 编译和默认检查。重构前基线 `b47a6ed` 已推送；恢复完整旧工程可使用该提交。旧算法按需求单独审查迁入。

字体和 uGUI 的来源及授权见 [第三方声明](THIRD_PARTY_NOTICES.md)。
