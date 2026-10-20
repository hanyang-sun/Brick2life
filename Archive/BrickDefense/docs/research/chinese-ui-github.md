# 中文界面与右侧战斗面板：GitHub 检索

日期：2026-10-11。用户“ui 改成中午”按上下文理解为“UI 改成中文”，并要求战斗开始入口移到右侧。

范围：地图、战斗设置/状态/结算、照片扫描/标定、已知错误提示汉化；中文字体随项目打包；战斗入口与状态卡片在右上状态卡片下方。验收为宽/窄屏中文不缺字、不裁切，右侧入口和输入拦截位置一致，参数设置、波次与照片流程继续可操作。

关键词：`github Unity simple localization MIT LocalizationManager.cs README`、`github noto sans CJK SimplifiedChinese OTF LICENSE SIL`。

## 候选比较与实际阅读

| 候选 | 阅读文件与版本 | 授权、维护与兼容 | 采用方式 |
| --- | --- | --- | --- |
| [3d-group/unity-simple-localization](https://github.com/3d-group/unity-simple-localization) | README.md、LICENSE、Runtime/Localization.cs；树 `ecb883d4d2f60e41da8fcfec32bd5e1d5a753791` | MIT，Copyright 2020 3D Group；最后推送 2024-02-21，未归档。读取本地 TextAsset JSON 为字典并按键查找，带语言选择、协程和 Newtonsoft.Json。机制可离线使用；直接包涉及 uGUI/TMP 等组件，当前 IMGUI 固定中文无需这些依赖。 | 仅参考文案与错误代码分离的键查找机制，集中翻译跨模块错误；不复制代码，不引入多语言管理器或包。界面标签直接使用中文，沿用现有深浅层级。 |
| [notofonts/noto-cjk](https://github.com/notofonts/noto-cjk) | 根 README.md、Sans/README.md、Sans/LICENSE；树 `f8d157532fbfaeda587e826d4cd5b21a49186f7c`；实际字体选 Sans/SubsetOTF/SC/NotoSansSC-Regular.otf | SIL Open Font License 1.1，可与软件捆绑，保留许可证与字体版权；字体不改名、不修改。仓库最后推送 2025-12-16，未归档。README 推荐区域子集静态 OTF，避免可变字体/字体集合支持成本。 | 直接整合 SC Regular 字体资产，Resources 加载到共享主题的所有 GUIStyle 与输入框。字体是离线资源，Unity 6 运行验证实际显示与字形覆盖；Android 仍需真机测试。 |

源码入口：[Localization.cs](https://github.com/3d-group/unity-simple-localization/blob/ecb883d4d2f60e41da8fcfec32bd5e1d5a753791/Runtime/Localization.cs)、[字体格式说明](https://github.com/notofonts/noto-cjk/blob/f8d157532fbfaeda587e826d4cd5b21a49186f7c/Sans/README.md)、[字体许可证](https://github.com/notofonts/noto-cjk/blob/f8d157532fbfaeda587e826d4cd5b21a49186f7c/Sans/LICENSE)。字体文件属于已发布字体实现，不是新增识别算法。

## 编码前整合方案

1. 将地图/战斗/扫描的可见标签、说明、状态和结算翻译为简体中文；扫描报告里的诊断代码、颜色 ID 与 API 字段保留，界面使用中文说明。
2. 在 Presentation 用集中错误翻译帮助类处理既有领域/平台返回的错误，不改变寻路、战斗、识别规则或存储格式。
3. 字体资源不修改，附完整 OFL 和来源记录；所有主题文字与输入框绑定同一资源字体，避免仅依赖开发机系统字体。
4. 把战斗卡片统一锚定在右侧，绘制和 ContainsPointer 共享同一矩形；原左侧区域恢复地图输入。参数弹窗继续使用现有居中表单。
5. 沿用实际 Game View 检查，核对右侧按钮、中文错误、输入拦截与宽窄屏截图；复核照片页与字体实际字形覆盖。不为静态文案镜像编写纯 C# 单元测试。

## 实施与验证

- 直接整合未修改的 Noto Sans SC Regular 2.004，8,331,336 字节；SHA-256 `faa6c9df652116dde789d351359f3d7e5d2285a2b2a1f04a2d7244df706d5ea9`。字体、Unity 导入元数据、来源记录与完整 OFL 已纳入工程，Resources 保留许可证副本用于播放器分发。
- 主题所有文字与输入框统一使用该字体；`ChineseUi` 集中处理稳定战斗错误代码与已知输入/相机消息，未引入候选的 Newtonsoft/uGUI/TMP 依赖或复制其源码。
- 战斗卡片位于右侧路线状态卡片下，绘制和 ContainsPointer 共用矩形；设置弹窗继续居中。
- Unity 6000.3.24f1 隔离验证：374 个源码中文字形均存在，右侧入口、中文参数错误、UI 输入隔离、多个入口战斗、胜负/重试通过；照片的真实取色、保存、10 块实拍识别、确认、裁切拒绝和窄屏滚动通过。两个进程均退出 0；截图与详细边界见 [验证记录](../../.scratch/chinese-ui/verification/README.md)。
