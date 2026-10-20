# 示例与照片关卡切换：GitHub 比较及修复方案

日期：2026-10-09。用户症状：两个示例只有地图，没有积木。

## 复现与范围

Unity Play Mode 的真实 `BrickDefenseDemo.Start` → `ApplyPhotoScan` → 示例按钮调用的 `LoadExample` 路径：
初始示例与照片关卡均有 9 个可绘制积木；切回示例后积木实体和场景子对象均为 0，
错误为 `UNMAPPED_COLOR`、`NO_CORE`。相机、材料和示例输入在此前阶段均通过。
需要恢复每种输入的颜色规则，使两个按钮在照片关卡之后仍然可用。

## 检索与实际阅读

关键词：`site:github.com Unity-Technologies open-project-1 SceneLoader GameStateSO`、
`site:github.com Brackeys Tower-Defense-Tutorial PauseMenu.cs LICENSE.md`。
本次读取日期的 main/master 内容，不声称固定到未经核实的提交。

| 候选 | 实际读取 | 授权与维护 | 适用部分与决定 |
| --- | --- | --- | --- |
| [Brackeys/Tower-Defense-Tutorial](https://github.com/Brackeys/Tower-Defense-Tutorial) | README.md、完整 LICENSE.md、`Tower Defense Unity Project/Assets/Scripts/PauseMenu.cs` | Unlicense。旧 Unity 教程，不能按现代运行依赖直接引入。 | `Retry`/`Menu` 恢复暂停状态，再加载对应场景；参考切换入口恢复目标场景上下文的组织。项目使用同一场景内的不同数据，故仅恢复目标布局的规则，不整场重载，不复制代码。 |
| [UnityTechnologies/open-project-1](https://github.com/UnityTechnologies/open-project-1) | README.md、LICENSE、`UOP1_Project/Assets/Scripts/SceneManagement/SceneLoader.cs` | Apache-2.0，README 明示项目已于 2021 年停止开发。 | `LoadMenu` 卸载旧 gameplay manager；新场景记录当前场景并在 ready 后启动玩法。参考清理旧上下文再启动新内容的顺序。Addressables、事件通道与多场景管理成本超出此次修复，不引入源码或依赖。 |

## 编码前确定的有限适配

仅修正 `LoadExample`：选择任一模拟示例时先恢复 `ExampleScans.CreateDemoRules()`，
再映射示例和构建关卡。照片规则保持在照片入口提供，保存的照片标定仍可用于下一次扫描。
这是既有 C# 数据接口的连接修复；没有第三方源码复制或新增运行库。
先只改规则恢复一项，再运行同一个 Play Mode 检查验证因果。

## 验证

`Assets/Editor/DemoSceneChecks.cs` 在隔离 Unity 项目中执行完整 Start、照片输入、两个示例入口与重复切换。
断言积木实体数、实际 Renderer、相机视锥、400 个底板格、路线和拒绝原因，并保存相机渲染图。
修复前 `reference-after-photo` 退出 1：`entities=0, rendered=0, issues=UNMAPPED_COLOR,...,NO_CORE`。
修复后要求普通示例有 9 块及 16 步路线；包围示例有 5 块且 `NO_ROUTE`，仍展示布局。

## 实际结果

只在 `LoadExample` 恢复示例的颜色规则，未改变视觉、映射、寻路或相机实现。
2026-10-09，Unity 6000.3.24f1 同一检查退出 0：初始普通示例、照片、照片后普通示例、
照片后包围示例、包围后普通示例均通过。普通示例 9 块/16 步；包围示例 5 块/`NO_ROUTE`。
相机渲染图已人工检查，积木及相应路线/出生点可见。
这些检查运行于独立的临时项目，用户当前编辑器保持运行。

## 源文件链接

- [Brackeys PauseMenu](https://github.com/Brackeys/Tower-Defense-Tutorial/blob/master/Tower%20Defense%20Unity%20Project/Assets/Scripts/PauseMenu.cs)、[Unlicense](https://github.com/Brackeys/Tower-Defense-Tutorial/blob/master/LICENSE.md)。
- [Unity SceneLoader](https://github.com/UnityTechnologies/open-project-1/blob/main/UOP1_Project/Assets/Scripts/SceneManagement/SceneLoader.cs)、[Apache-2.0](https://github.com/UnityTechnologies/open-project-1/blob/main/LICENSE)。
