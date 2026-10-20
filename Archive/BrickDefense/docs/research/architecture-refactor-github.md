# 架构划分重构：GitHub 比较与适配

日期：2026-10-09。用户批准实施 Domain、Vision、Application、Presentation、Infrastructure 划分。
目标：集中更新关卡上下文，拆出绘制与界面，拆出照片输入和保存，以 asmdef 检查依赖；保持示例、照片和无路预览行为。

## 检索及实际阅读

关键词沿用 `Unity open-project-1 SceneLoader`、`Brackeys Tower-Defense-Tutorial BuildManager Node`。
本次完整读取 BuildManager.cs、Node.cs、SceneLoader.cs；README 与完整许可证已在本会话的示例切换研究中读取，见 `demo-switching-github.md`。

| 候选 | 实际读取与许可证 | 适用部分与决定 |
| --- | --- | --- |
| [Brackeys/Tower-Defense-Tutorial](https://github.com/Brackeys/Tower-Defense-Tutorial)，master 读取版本 | README.md、LICENSE.md（Unlicense）、BuildManager.cs、Node.cs、PauseMenu.cs | 选择状态与显示/点击分开组织。采用职责分离的思想；其静态 singleton、经济与建塔逻辑不匹配本项目，不复制源码、不引入资产。 |
| [UnityTechnologies/open-project-1](https://github.com/UnityTechnologies/open-project-1)，main 读取版本 | README.md、LICENSE（Apache-2.0）、SceneLoader.cs | 上下文切换后通过通知启动新显示流程，并明确监听生命周期。采用上下文整体切换、变更通知与注销监听的组织；不采用 Addressables、多场景和 ScriptableObject 事件总线，不复制源码。 |

两者均为旧教程/已停止开发项目，只作为已阅读的设计依据，不引入运行时依赖。
[Unity 官方程序集引用文档](https://docs.unity3d.com/cn/6000.0/Manual/assembly-definitions-referencing.html)用于核对 asmdef 显式引用、循环依赖与 Editor/运行时隔离。

## 编码前确定的方案

- `LevelSession` 是关卡状态的唯一写入口，`LevelContext` 同时携带布局、规则、出生点及计算结果；两个示例与照片均跨过该接口。
- `LevelView` 绘制已有 `LevelResult`，`DemoUI` 发出用户操作，`BrickDefenseDemo` 保留原脚本 GUID 并作为接线入口。
- `PhotoScanSession` 管理照片分析、耗时、结果与对应标定，确认时整体产生布局定义；界面不单独拼接扫描结果和后来修改的规则。
- Infrastructure 接管 PNG/JPG 解码、相机权限/生命周期、PlayerPrefs 标定与报告文件读写；Presentation 只处理界面与预览纹理。
- Domain、Vision、Application 使用纯 C#。为保持现有 ScanResult/evidence 数据合同，共享 DTO 放入 Contracts 基础程序集；图像帧与标定仍在 Vision 实现中。
- 第三方代码保留原版源码和授权，合为算法程序集，通过 InternalsVisibleTo 向实际使用模块开放既有内部类型，不修改原堆实现。
- 建立 Contracts、Domain、Vision、Application、Infrastructure、Presentation、Algorithms 和 Editor 的 asmdef，纯逻辑模块关闭引擎引用。
- 保留当前类的 namespace/GUID 兼容必要入口，更新场景的程序集名称与 .NET 检查路径。

## 验收

已有 .NET 检查重构前通过。增加 LevelSession 的两个输入反复切换、非法输入保留状态、输入规则快照及出生点变更检查；增加 PhotoScanSession 的原图分析与确认检查。
Play 检查改用公开关卡接口和实际场景对象，移除旧的私有字段反射；核对 9 块/16 步与包围 5 块/NO_ROUTE、实际渲染、照片 PNG 与持久化接入。
在隔离 Unity 项目编译全部 asmdef，并验证场景脚本 GUID、播放器程序集不引用 UnityEditor。

## 源链接

- [BuildManager](https://github.com/Brackeys/Tower-Defense-Tutorial/blob/master/Tower%20Defense%20Unity%20Project/Assets/Scripts/BuildManager.cs)、[Node](https://github.com/Brackeys/Tower-Defense-Tutorial/blob/master/Tower%20Defense%20Unity%20Project/Assets/Scripts/Node.cs)。
- [SceneLoader](https://github.com/UnityTechnologies/open-project-1/blob/main/UOP1_Project/Assets/Scripts/SceneManagement/SceneLoader.cs)。

## 实施结果

五个主要模块及 Contracts、Algorithms、Editor 程序集已建立，实际引用与设计一致。没有复制两个候选项目的源码，也没有引入其依赖或资产；两者在本项目中的用途是职责分离与状态变更通知的组织依据。
照片面板已委托 PhotoScanSession、PhotoFileSource、CameraCapture、PhotoProfileStore 和 ScanReportStore。场景入口只连接模块，视图只绘制 LevelResult，操作通过 LevelSession 更新一组完整上下文。
程序集划分保留原 MonoBehaviour GUID 和标定格式；既有第三方源码与许可证保留。

### 验证结果（2026-10-09）

- `dotnet run --project Tests/BrickDefense.Checks.csproj`：退出 0。关卡、200 个独立 BFS 对照布局、照片与颜色回归，以及新增 WorkflowChecks 全部通过。
- 隔离 Unity 6000.3.24f1 项目，执行 `BrickDefense.Editor.DemoSceneChecks.VerifyExamples`：退出 0，`DEMO_SCENE_CHECKS_PASSED`。
- `ARCHITECTURE_ASSEMBLIES_PASSED`：Contracts、Algorithms、Domain、Vision、Application 无 Unity 引用，运行时 Infrastructure/Presentation 无 UnityEditor 引用。
- 六次示例/照片切换：初始示例、照片、照片后普通示例、第二次照片、照片后包围示例、包围后普通示例。普通示例 9 块且 16 步，包围示例 5 块且 NO_ROUTE，实际活跃 Renderer 和相机渲染通过。
- 原生成 PNG 经文件读取、标定、异步识别、公开确认接口及渲染通过：6 蓝、2 红、1 绿，91 空格。
- `ARCHITECTURE_IO_PASSED`：设置加载、独立测试键的标定读写、扫描报告、原文件字节和归一 PNG 逐像素对照通过。非法输入保留当前关卡。
- `.meta` 齐全，原项目与验证项目的已有资源元数据相同。

测试代码已移除旧控制器的私有字段/方法反射，调用与界面一致的公开入口；程序集检查只读取编译后引用信息。
日志与渲染截图保存在 `.scratch/architecture-refactor/` 的验证目录中，独立 Unity 缓存不纳入版本控制。
硬件相机、Android 构建和实拍准确率未验证。两个参考项目的组织思路没有增加网络或 Android 运行时依赖。
