# Brick2Life

**让积木布局成为游戏世界的输入。**

Brick2Life 是一个基于 Unity 的实体积木交互与虚实融合塔防项目。项目希望让玩家通过摆放、移动和移除桌面上的积木来设计关卡，由顶部相机感知布局变化，更新游戏路线与状态，再通过桌面投影把反馈呈现在实体场景中。

项目结合计算机图形学、计算机视觉与实体交互，围绕“积木布局如何被感知、如何驱动游戏、如何与投影对齐”构建完整流程。

> 当前处于开发阶段。截至 2026-10-19，`main` 保留可运行的单张照片塔防原型；Brick2Life 单层仿真工作台位于 `feat/task-a-unity-bricks-camera` 分支。固定桌面坐标下的识别、布局变化检测与投影反馈尚未接通。

2026-10-19 仿真扩展：顶置采集相机采用透视投影；Play 后点击右侧“光照：双侧 · 标准”，可选择左侧/右侧/双侧白光、三档亮度、左右强度比例、环境光倍数和阴影开关。默认双侧标准亮度、比例 50:50、环境 1 倍，小窗口可滚动操作。采集 JSON 顶层 schema 3、光照 schema 2 同时记录请求设置与实际生效值；使用方式与验证边界见 [光照说明](docs/task-a-lighting-guide.md)、[相机元数据说明](docs/task-a-camera-metadata-guide.md) 与 [项目功能汇总](docs/implemented-features-2026-10-18.md)。

## 项目目标

```text
虚拟积木搭建与仿真验证
          ↓
顶部相机采集实体布局
          ↓
固定棋盘坐标下的积木识别
          ↓
布局变化检测与确认
          ↓
塔防地图、路线与游戏状态更新
          ↓
桌面投影反馈
```

- **积木搭建与仿真：**在 Unity 中编辑虚拟积木，生成相机图像和对应的真实布局标注，为识别与评价提供样本。
- **固定坐标识别：**将相机图像映射到固定棋盘格位，识别积木位置与颜色，并将颜色与游戏角色分开配置。
- **布局变化检测：**比较前后两次确认的布局，处理增删、移动、改色，以及无法唯一匹配的情况。
- **塔防游戏联动：**将确认的积木布局转换为核心、城墙和防御塔，计算路线、射程并运行波次战斗。
- **桌面投影反馈：**把路线、射程等平面信息映射回实体桌面，验证显示位置与实际布局的对齐效果。

第一阶段聚焦 **10×10 棋盘、单层、等大积木与按需采集**。玩家移开手后主动触发更新，战斗使用开局时确认的布局快照。多层堆叠、连续识别和对局中实时改图属于后续扩展。

## 当前进度

| 模块 | 已有能力 | 所在位置与状态 |
| --- | --- | --- |
| 单层仿真工作台 | 三维操作主视图、固定顶置相机预览、积木增删与拖拽、可配置颜色、清空棋盘 | 已实现，位于 [仿真开发分支](https://github.com/hanyang-sun/-/tree/feat/task-a-unity-bricks-camera) |
| 仿真样本导出 | RGB PNG 与真值 JSON 配对保存，包含全部格位、积木 ID、颜色、布局版本和棋盘四角坐标 | 已实现，位于仿真开发分支 |
| 单张照片塔防原型 | 照片导入、颜色标定、相对布局识别、人工校对、关卡生成、多入口寻路与波次战斗 | 已实现，保留在 `main` |
| 固定棋盘识别与变化检测 | 真实相机标定、固定格位恢复、前后布局差异与歧义处理 | 待开发与联调 |
| 游戏与投影闭环 | 根据新观测更新游戏、输出平面反馈、完成真实桌面对齐验证 | 待开发与联调 |

仿真 JSON 是 Unity 已知布局的**真值**，供后续算法评价使用。现有照片原型输出的是相对格位，固定物理坐标与真实相机标定仍需实现。

## 体验 Brick2Life 仿真工作台

### 环境与启动

使用 **Unity 6000.3.24f1**，工程采用 Built-in 渲染管线。首次体验可将仿真分支克隆到独立目录：

```powershell
git clone --branch feat/task-a-unity-bricks-camera https://github.com/hanyang-sun/-.git Brick2Life
```

1. 在 Unity Hub 中添加并打开克隆得到的 `Brick2Life` 目录。
2. 打开 `Assets/Scenes/Brick2LifeSimulation.unity`。
3. 点击 Play，使用侧栏工具编辑积木并观察右侧顶置相机预览。

仿真场景需要手动打开；当前 Build Settings 中的默认构建入口仍为旧场景 `Assets/Scenes/BrickDefenseDemo.unity`。

### 可体验内容

- 在 10×10 棋盘上选择颜色，使用添加、删除和拖拽工具编辑单层积木。
- 移动时自动对齐格位，目标被占用或越界时取消移动；移动成功后保留积木 ID。
- 使用“清空棋盘”重新搭建，也可以采集空棋盘样本。
- 点击“保存 RGB + 标注”，导出同名 PNG 与 JSON；操作界面与固定实验相机的图像相互独立。

### 导出数据

Unity 编辑器将样本保存到工程根目录的 `SimulationCaptures/`；Player 使用 `Application.persistentDataPath/SimulationCaptures/`。

每对样本包含：

| 文件 | 内容 |
| --- | --- |
| `rgb_*.png` | 固定顶置相机采集的 RGB 图像 |
| `rgb_*.json` | 帧 ID、时间、图像尺寸、布局版本、全部 100 格的占用状态，以及积木 ID、颜色和层号 |
| JSON 中的棋盘信息 | 棋盘原点、格边长、坐标约定，以及四个边界角点的格网坐标、世界坐标与图像像素坐标 |

逻辑 `gridX/gridY` 对应 Unity 世界 `X/Z`，世界 `Y` 表示高度；当前为单层 `layer=0`。角点对应关系来自已知的仿真几何与相机投影，真实设备仍需独立标定。

## 保留的塔防原型

默认 `main` 分支保留 BrickDefenseMVP，作为 Brick2Life 后续游戏适配的基础。使用相同 Unity 版本打开工程，运行 `Assets/Scenes/BrickDefenseDemo.unity`，即可体验：

- 无格纸照片导入、背景与角色颜色标定、相对布局识别。
- 人工校对积木数量、颜色与位置，记录识别和格位恢复正确率。
- 将 10×10 积木布局转换为 20×20 游戏格，配置多个出生点并预览四方向最短路线。
- 设置波次、运行塔防战斗，以及暂停、重试与胜负结算。

原型识别面向简单背景、尺寸一致、单层且尽量正上方完整入镜的照片，通用实拍准确率仍待验收。旧版完整使用说明见 [BrickDefense v1.3.1 README](https://github.com/hanyang-sun/-/blob/brickdefense-v1.3.1/README.md)。

## 后续开发

1. 完成固定棋盘坐标下的识别，并使用仿真真值评价格位与颜色结果。
2. 建立布局变化检测、确认与失败恢复流程；识别拒绝时保留上一份确认布局。
3. 将确认的布局接入塔防地图和寻路，输出路线与射程等平面反馈。
4. 接入真实相机与投影仪，记录响应耗时并测量桌面中心和边缘的投影对齐误差。

详细模块边界、三人分工及首阶段验收条件见 [Brick2Life 开发方案](docs/brick2life-development-plan.md)。该方案记录初始规划，具体已实现内容以对应分支代码为准。

## 开发与文档

纯 C# 逻辑检查需要 **.NET 10 SDK**，在 Unity 工程根目录运行：

```powershell
dotnet run --project Tests/BrickDefense.Checks.csproj
```

仿真开发分支还可单独运行布局检查：

```powershell
dotnet run --project Tests/BrickDefense.Checks.csproj -- --simulation-layout-checks
```

- [Brick2Life 开发方案与验收条件](docs/brick2life-development-plan.md)
- [现有原型架构](docs/architecture.md)
- [仿真模块源码](https://github.com/hanyang-sun/-/tree/feat/task-a-unity-bricks-camera/Assets/Scripts/Simulation)
- [第三方代码与许可证说明](THIRD_PARTY_NOTICES.md)
