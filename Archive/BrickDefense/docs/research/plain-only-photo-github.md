# 照片入口收敛为无格纸：GitHub 检索与裁剪方案

日期：2026-10-11。用户已实测有格纸模式多次失败，要求删除该模式。范围是移除有格纸入口、路由、格纸导出和专用识别代码，直接复用既有无格纸识别与标定。此前的格纸试验不作为当前功能保留。

检索关键词：`site:github.com/thomas-rooty/lego-detector lego_detector.py`、`site:github.com/opencv/opencv threshold_inRange.py`。实际重新读取两个候选的 README、完整相关源码和许可证；读取固定版本，不宣称为当前最新实现。

| 候选与版本 | 实际阅读、授权与维护 | 匹配、兼容及采用 |
| --- | --- | --- |
| [thomas-rooty/lego-detector](https://github.com/thomas-rooty/lego-detector/tree/209eb05aaf6525da2736a59aeecb97fed8370c82) | README.md、lego_detector.py、LICENSE；MIT，Thomas Caron 2023。固定读取版本较旧，无维护保证。 | 明确使用背景分割、连通对象与框选，不依赖格纸。其 Python/Flask/TensorFlow 和额外空背景照片不适合当前 Unity 离线入口；继续参考已有检测组织机制，不复制源码或引入服务。 |
| [opencv/opencv 4.12.0](https://github.com/opencv/opencv/tree/4.12.0) | README.md、LICENSE、完整 samples/python/tutorial_code/imgProc/threshold_inRange/threshold_inRange.py；Apache-2.0，固定稳定版本。 | 通过可配置颜色范围产生掩码，支持相机帧输入。Python/C++ 与 Unity C# 绑定/原生平台打包有额外依赖成本；继续复用项目现有色样分类，不加入库或复制示例。 |

两者都支持把照片分成背景与对象后再检测。当前 `PlainBackgroundRecognizer` 已按此前 [无格纸检索](markerless-photo-github.md) 建立相同流程，本次只将该流程设为唯一入口。Unity/Android 可离线处理是本项目适配判断，尚无 Android 真机验收结果。

## 编码前方案

1. 删除模式切换控件、格纸拍摄提示、相机方形格纸导引和格纸导出菜单。照片直接采样背景、核心、城墙、防御塔，生成相对网格。
2. `PhotoScanSession` 直接调用既有无格纸识别器，移除双模式分支。默认标定键采用已有 `BrickDefense.PhotoCalibration.plain.v1`，四颜色数组格式继续使用。
3. 将仍需要的设置验证移入无格纸识别器；删除格纸定位、投影拟合、逐格识别、模板、ArUco 依赖及专用参数。颜色 Lab 模块继续使用既有 Colourful 适配。
4. 当前扫描报告继续写 `inputMode=PlainBackground`、`coordinateBasis=RelativeBrickScale`，保留照片、像素证据和报告字段。原始报告留作历史记录。
5. 删除格纸专用回归与示例生成入口，工作流与编辑器检查使用原实拍样本。验证默认入口、既有标定恢复、真实取色、10 块识别、生成关卡、拒绝保留地图与宽窄屏；补充规格说明取代 v1 的格纸要求。

本次新增外部源码采用范围为零，无新增许可证义务；删除 ArUco 后移除其当前分发声明，旧检索记录仍作为历史依据。

## 完成结果

已移除双模式 UI/API、格纸导出、格纸定位/投影/逐格识别、ArUco 和专用阈值；背景标定沿用原来的无格纸键。纯 C# 全套检查和 Unity 默认入口实际操作均退出 0，完整原实拍仍为 10 块，裁切输入继续拒绝；既有标定恢复、相对报告和宽窄屏操作通过。详细结果与截图见 [验证记录](../../.scratch/plain-only-photo/verification/README.md)。
