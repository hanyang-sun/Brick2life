# 日常识别测试集自动生成：GitHub 调研

日期：2026-10-20。编码前完成检索、README、实现入口与许可证核对。

## 当前需求

用户选择默认 **100 张**，用于日常识别测试。采用 20 个不同棋盘布局 × 5 个光照条件；数量、布局类型和种子可配置。此前撤回的大规模全组合方案保持撤回。本次不引入第三方生成框架。

## 候选比较

| 项目 | 实际阅读 | 适用机制 | 本项目判断 |
| --- | --- | --- | --- |
| [Unity Perception](https://github.com/Unity-Technologies/com.unity.perception) | [README](https://github.com/Unity-Technologies/com.unity.perception/blob/main/README.md)、[FixedLengthScenario.cs](https://github.com/Unity-Technologies/com.unity.perception/blob/main/com.unity.perception/Runtime/Randomization/Scenarios/FixedLengthScenario.cs)、[ScenarioBase.cs](https://github.com/Unity-Technologies/com.unity.perception/blob/main/com.unity.perception/Runtime/Randomization/Scenarios/ScenarioBase.cs)、[Apache-2.0 许可证](https://github.com/Unity-Technologies/com.unity.perception/blob/main/LICENSE.md) | 显式迭代上限、随机种子、每轮帧数及迭代生命周期 | README 已声明停止维护，目标 Unity 版本与本项目 Unity 6 不同。借鉴有限迭代和种子机制；复用项目已有 CaptureFrame，不安装该包。 |
| [BlenderProc2](https://github.com/DLR-RM/BlenderProc) | [README](https://github.com/DLR-RM/BlenderProc/blob/main/README.md)、[basic/main.py](https://github.com/DLR-RM/BlenderProc/blob/main/examples/basics/basic/main.py)、[GPL-3.0 许可证](https://github.com/DLR-RM/BlenderProc/blob/main/LICENSE) | 明确设置场景、灯光和相机，再统一渲染、写出对应数据 | 需要 Blender 运行环境；不适配现有 Unity 场景和 JSON。参考计划／渲染／输出的分工，不复制其代码。 |

## 有限借鉴及实现边界

- Application 层生成确定性的有限布局计划；Core 棋盘合同保持通用，实验参数不进入棋盘定义。
- Simulation 层增加整盘快照替换，继续使用现有材质、灯光控制及 RGB + schemaVersion=3 的 GT 采集。GT 只供评测，B 的识别输入仍为 RGB 与约定公开标定。
- Editor 层提供设置、后台进度及取消；独立采集工程使用场景副本，不修改当前编辑器中的布局或光照。
- 每次运行使用新目录；先完成图片与 GT 配对写入，再增加清单记录。失败／取消保留已完成样本并明确标记状态。
- 同一棋盘的不同光照帧使用同一 layoutGroupId，后续划分训练与测试时应整组分配。100 张对应 20 个布局，不能当作 100 个独立布局。
- 此次只覆盖现有几何、颜色与光照能力；不宣称已经覆盖真实相机噪声、镜头畸变、投影干扰或识别准确率。

没有引入第三方源码、包或资产，因此无需新增第三方运行依赖或搬入上述许可证。配置与来源核对日期随实现文档保留。
