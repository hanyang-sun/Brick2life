# Brick2Life

Unity 中的积木交互与视觉反馈实验平台。当前实现 10×10 单层积木工作台、固定透视相机、可配置光照、RGB/真值配对采集，以及 RGB 识别、稳定确认、关卡反馈、塔防战斗和虚拟投影。真值仅用于评价，不参与视觉预测或开战正确性判断。

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
| Application | 确认会话、帧/标定校验、有限测试集布局计划；无 Unity 依赖 | 集成负责人 |
| Simulation | 独立棋盘会话、编辑界面、相机/光照采集和场景接线 | A |
| Vision | 固定格位识别：调色板色样、逐格顶面采样与归一化 RGB 分类、连续帧稳定与变化事件、与真值的准确率对照；无 Unity 依赖 | B |
| Feedback | 关卡语义映射（颜色→核心/城墙/塔）、游戏对象更新、表面贴合的虚拟投影与场景分层；只消费 `BoardUpdate` | C |
| Battle | 关卡转 20×20 战斗格、A* 寻路、波次/塔/核心模拟与暂停重试；无 Unity 依赖 | 新管线 |

任务 B 与任务 C 已接线：识别面板持有唯一的 `BoardSyncSession`，确认后的 `BoardUpdate` 同时驱动关卡对象与投影，人工目标演示在识别接入后自动停用。自动识别启用时，点击开始后等待新 RGB 稳定确认；战斗中可继续放置、删除和移动积木，确认后保留本局进度并从敌人当前位置重新寻路。无核心、入口被占或堵路时等待修正，恢复有效布局后继续；出生点在本局保持固定。编辑器 Play 模式打开原始布局与只读投影两个窗口，展示窗口可旋转缩放，并切换“积木＋投影／仅投影”。角色纹样与战斗动态效果均纳入投影采集开关。使用见 [实时交互指南](docs/live-battle-editing-guide.md)、[双窗口指南](docs/editor-dual-window-guide.md) 和 [A/C 整合说明](docs/a-c-integration.md)。Core 的 `Contracts/` 是公共合同唯一来源。接入方式见 [架构说明](docs/architecture.md)。

## 采集数据

编辑器输出到根目录 `SimulationCaptures/`；Player 输出到 `Application.persistentDataPath/SimulationCaptures/`。每次写入同名 PNG/JSON，JSON 最后发布，包含全部 100 格、帧 ID、布局版本、棋盘四角、相机与光照配置。JSON 顶层 schema 3、相机 schema 1、光照 schema 2 保持兼容。

`gridX/gridY` 对应 Unity 世界 `X/Z`，高度为世界 `Y`；当前 `layer=0`、格长 1 仿真单位。图像原点在左上，数组按 `cells[y*width+x]` 排列。颜色与玩法角色独立。

GT 仅用于导出和评价。B 接收图像与约定公开配置，C 消费视觉确认状态。遮挡/未知不视为空格，相同布局不会产生新的确认版本。详见 [相机元数据](docs/task-a-camera-metadata-guide.md)、[光照控制](docs/task-a-lighting-guide.md)。采集数据及 Unity 验证缓存保存在本机。

菜单 **Brick2Life → Dataset Generator** 可自动生成日常识别测试集，默认 **20 个不同布局 × 5 种光照 = 100 张**，支持数量、种子、布局类型和取消。后台使用独立采集工程；配对图像、GT 及运行清单输出到 `SimulationCaptures/Datasets/`。也可执行 `pwsh -File Tests/GenerateDataset.ps1`。详见 [测试集生成指南](docs/task-a-dataset-generation-guide.md)。

## 检查与构建

纯 C# 检查需要 .NET 10 SDK，默认包含布局、快照、确认会话、数据集计划及任务 C 的关卡语义／投影计划／反馈会话（共 463 项断言）：

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
