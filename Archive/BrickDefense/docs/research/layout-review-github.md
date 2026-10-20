# 地图校对：GitHub 检索与有限借鉴

日期：2026-10-12。关键词：Unity runtime grid map editor MIT、Unity level editor runtime tile editor。

## 范围

照片识别后生成可编辑草稿。先对照照片核对实物数量、颜色、格位，保存原始结果和人工真值并统计两项正确率；随后自由设计游戏布局，整体平移、增删、改色与单块移动不改写已确认的评价依据。支持撤销、恢复、失败保留旧关卡。关卡可玩性检查在发布与选择出生点阶段执行。

## 实际阅读与比较

| 候选 | 阅读文件 | 授权与依赖 | 判断 |
| --- | --- | --- | --- |
| [Hertzole/advanced-level-editor](https://github.com/Hertzole/advanced-level-editor) | README、LICENSE、Packages/se.hertzole.ale/Runtime/Core/LevelEditor.cs 前 100 行 | MIT，部分附带 Unity 授权代码；包含模式、选取、撤销、保存模块与代码编织，已归档 | 借鉴编辑模式与最终提交分离、编辑状态集中管理。完整包的代码编织及资源界面超出本项目需求。 |
| [SinlessDevil/UnityGridLevelEditor](https://github.com/SinlessDevil/UnityGridLevelEditor) | README、LICENSE、Assets/Code/LevelEditor/Editor/Grid/GridMoveController.cs 前 100 行、Editor/BlockPopup.cs 前 100 行 | MIT；移动/选择界面引用 UnityEditor、UIElements，README 列有 Odin 与 DOTween | 借鉴网格选中后移动、角色面板、整体位移。采用点选源格再点目标格适配 IMGUI 和手机；目标占用时保留草稿并提示。 |

README/许可证经 GitHub 浏览读取；相关原始源码经 raw.githubusercontent.com 读取。代码阅读范围如上，未将未阅读部分视为已验证功能。

## 整合方案

采用机制参考，未复制源码或引入包。复用本项目的 ScanResult、BrickObservation、GridMapper、LevelSession、SoftStrategyTheme 与照片输入，增加纯 C# LayoutEditSession 集中处理编辑、历史、真值冻结和比较，Presentation 提供中文点选网格、照片对照及三维预览，Infrastructure 保存独立 review JSON。

识别器保留可恢复错误下已检测的积木和诊断；原始 Rejected 状态不转换为自动成功。玩家确认后重新建立一致的 100 格合同。指标按稳定积木 ID 匹配，补加表示漏检、删除表示误检；颜色与位置分别评价，位置采用预先固定的 min-column/min-row → 1 相对坐标约定，整体平移不影响相对准确率。没有人工真值时不显示准确率。

## 验证

纯 C# 检查一对一身份、漏检/误检、改色/移位、整组平移、撤销、边界和重叠、不可变真值与原始证据、缺核心草稿及发布保护。Unity 验证真实 JPG 的校对、两阶段界面、记录、关卡提交与宽窄屏实际渲染。
