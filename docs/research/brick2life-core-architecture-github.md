# Brick2Life 独立核心与旧系统退役：检索与适配

日期：2026-10-20。依据用户已确认的模块安排和退役范围；重构前基线 `b47a6ed` 已推送到 GitHub。目标是保留单层仿真行为、相机/光照采集格式，移出旧系统，建立无 Unity 的棋盘核心和确认会话。识别算法、真实设备和投影渲染不在本轮实现范围。

## 检索与实际阅读

关键词：`github ardalis CleanArchitecture Core UseCases Infrastructure csproj`、`github UnityTechnologies open-project-1 SceneLoader asmdef`。检索后直接阅读下列原仓库文件，而非仅依据搜索摘要。

| 候选 | 实际读取 | 适用性与采用决定 |
| --- | --- | --- |
| [ardalis/CleanArchitecture](https://github.com/ardalis/CleanArchitecture) | [README](https://github.com/ardalis/CleanArchitecture/blob/main/README.md)、[LICENSE](https://github.com/ardalis/CleanArchitecture/blob/main/LICENSE)、[Core 工程](https://github.com/ardalis/CleanArchitecture/blob/main/src/Clean.Architecture.Core/Clean.Architecture.Core.csproj)、[UseCases 工程](https://github.com/ardalis/CleanArchitecture/blob/main/src/Clean.Architecture.UseCases/Clean.Architecture.UseCases.csproj) | MIT。采用核心不依赖外部实现、应用只引用核心、入口组合具体实现的组织机制。原项目面向 ASP.NET，框架与 NuGet 依赖不适用于 Unity/Android；不引入模板、MediatR、EF 或源码。 |
| [UnityTechnologies/open-project-1](https://github.com/UnityTechnologies/open-project-1) | [README](https://github.com/UnityTechnologies/open-project-1/blob/main/README.md)、[LICENSE](https://github.com/UnityTechnologies/open-project-1/blob/main/LICENSE)、[SceneLoader.cs](https://github.com/UnityTechnologies/open-project-1/blob/main/UOP1_Project/Assets/Scripts/SceneManagement/SceneLoader.cs) | Apache-2.0。2021 年已停止开发，面向 Unity 2020.3，仅参考集中流程控制、防止重复请求、成对注册/注销的机制。不引入 Addressables、多场景框架、ScriptableObject 事件总线或源码。 |

本轮仅作机制参考；纯 C# 数据/会话代码由本项目实现，不增加网络、原生或 NuGet 运行时依赖，避免影响离线运行及未来 Android 构建。

## 有限适配与验收

- Core 内含 Contracts，集中固定棋盘定义、颜色、逐格确定状态、不可变快照、观测和差异；采集 DTO 保持为 Unity 序列化适配。
- 将已有 SimulationLayout 发展为纯 C# 布局模型，由场景布局会话持有；编辑器不再拥有唯一状态，采集读取独立快照。
- Application 的 BoardSyncSession 验证帧顺序、标定及完整观测，先计算后确认；未知、拒绝、取消与过期结果保留上一份确认状态。它不接收仿真 GT。
- Simulation 保留现有脚本 GUID、配色、灯光及 schema 3 导出行为。Editor 独立，仅编辑器可引用。
- Vision/Projection 的具体实现由 B/C 接入，不创建空程序集或伪造已完成的识别闭环。
- 旧运行时、场景、编辑器、测试、算法及授权配对归档到 Assets 外；保留字体、uGUI、仿真材质、用户采集与实验记录。
- 验收：现有布局回归、快照与确认会话的失败路径检查；Unity 完整程序集编译、原场景光照/相机采集回归、Windows Player 构建。结果记录在架构说明的验证章节。
