# 识别采样模式按钮：复用比较

日期：2026-11-05。需求：在当前搭建界面显示“带投影 / 不带投影”的采样模式，点击切换，与已有 F8 保持同步。保留展示投影和现有布局。

检索词：`Unity-Technologies/uGUI Button onClick`、`UnityCsReference UIElements Button clicked`。

| 候选 | 实际阅读 | 授权与取舍 |
| --- | --- | --- |
| [Unity-Technologies/uGUI](https://github.com/Unity-Technologies/uGUI) | `com.unity.ugui/README.md`、`com.unity.ugui/LICENSE.md`、`com.unity.ugui/Runtime/UGUI/UI/Core/Button.cs` | Unity Companion License。已是工程依赖，直接复用 `Button.onClick` 和本地 `CreateButton`，不复制上游源码、不增加包。与当前 Unity 6 / Canvas 界面一致，运行时不需要联网。 |
| [Unity-Technologies/UnityCsReference](https://github.com/Unity-Technologies/UnityCsReference) | `README.md`、`LICENSE.md`、`Modules/UIElements/Core/Controls/Button.cs` 的 `clicked` 事件 | Unity Reference Only License，仅阅读参考。UI Toolkit 需要另一套界面树，本次不引入、不复制代码。上游当前是 7000 系列，不能代替本机 6000.3.24f1 验证。 |

适配：沿用识别按钮的字体、尺寸和父 Canvas，将新按钮放在识别按钮旁边，文字显示当前模式及 F8 提示。按钮和 F8 调用同一切换方法，唯一状态仍是 `SimulationCameraCapture.CaptureWithoutProjection`；使用现有 setter 取消未完成采样。外部改变属性后，按钮在下一次界面刷新时同步。隐藏识别结果面板不隐藏采样按钮。

验证：在既有隔离 Unity 双窗口检查中覆盖按钮点击、待采样取消、共用快捷键处理入口、外部模式改变和面板隐藏后的可用性。模式切换不改变观众投影可见状态。该按钮只控制仿真相机采样是否包含投影，不声称完成真实硬件消隐或补偿精度验证。

实测：Unity 6000.3.24f1 隔离双窗口检查 234 项通过，包含新增的 8 项模式按钮交互断言。证据位于本机 `.scratch/dual-window/run-20261105-230834-400/verification-SimulationDualWindowChecks.json`，失败项为空。
