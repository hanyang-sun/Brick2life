# 珊瑚屏障的格位拼接

2026-11-07 在 GitHub 查阅了 RuleTile terrain、orthogonal and diagonal neighbors、Wang tile edge and corner matching 的相关实现。目标是在已确认棋盘上生成墙顶图案，相邻墙共享细缝，内外拐角各有对应装饰，敌人经过时只提亮朝向它的珊瑚。

| 候选 | 阅读内容与许可 | 适配结论 |
| --- | --- | --- |
| [Unity 2D Extras](https://github.com/Unity-Technologies/2d-extras) | [README](https://github.com/Unity-Technologies/2d-extras/blob/master/README.md)、[RuleTile.RuleMatches](https://github.com/Unity-Technologies/2d-extras/blob/master/Runtime/Tiles/RuleTile/RuleTile.cs#L678)、[Unity Companion License](https://github.com/Unity-Technologies/2d-extras/blob/master/LICENSE.md) | C# 的旋转邻接条件与本项目匹配。参考按邻居选图的机制，继续使用现有投影片和材质，因此无需 Tilemap 包或瓦片资源。 |
| [Tiled](https://github.com/mapeditor/tiled) | [README](https://github.com/mapeditor/tiled/blob/master/README.md)、[WangId 的边角编码与相邻匹配](https://github.com/mapeditor/tiled/blob/master/src/libtiled/wangset.cpp#L35)。该源文件使用 BSD 2-Clause 条款，编辑器另有 [GPL 许可](https://github.com/mapeditor/tiled/blob/master/LICENSE.GPL)。 | 参考分别表达边和角的做法。C++/Qt 编辑器与项目的 Unity 运行时、离线及 Android 目标没有直接集成价值。 |

从已确认墙格查四个正交邻居。只有两侧都连接时才检查相应对角，以区分内凹角与实心连接。渲染继续复用现有墙顶面片和内容相机，使用程序图案和敌人位置驱动珊瑚亮度。两项参考均限于设计思路，无外部源码、资源和包依赖。用 L 形拼接、对角孤立墙、墙移除、邻敌与无敌的对照图和顶置相机识别验证。

## 扇贝盾徽

2026-11-07 在 GitHub 对照了两种 24×24 图标，检索词为 `shell icon SVG` 和 `filled shield SVG`。验收关注默认 512×512 投影输出和绿色墙砖反射后的辨识度。

| 候选 | 阅读内容与许可 | 取舍 |
| --- | --- | --- |
| Lucide | [README](https://github.com/lucide-icons/lucide/blob/main/README.md)、[Shell 源文件](https://github.com/lucide-icons/lucide/blob/main/icons/shell.svg)、[ISC 许可](https://github.com/lucide-icons/lucide/blob/main/LICENSE) | 单路径描边的螺旋壳适合界面图标。在墙顶约二十像素的徽记中，扇形外轮廓和宽放射纹更适合表达扇贝。 |
| Tabler Icons | [README](https://github.com/tabler/tabler-icons/blob/main/README.md)、[实心盾牌源文件](https://github.com/tabler/tabler-icons/blob/main/icons/filled/shield.svg)、[MIT 许可](https://github.com/tabler/tabler-icons/blob/main/LICENSE) | 实心盾形在小尺寸仍易辨认。采用同类的实心轮廓思路，复用现有墙顶着色器和邻接掩码，可离线运行且无需 SVG 导入或新资源。 |

采用已经试投确认的程序图案，按四边暴露情况为孤立墙和 L 形拐角添加盾徽。浅金扇贝用完整贝缘和两道内部放射纹，深色内衬与窄边帮助分离盾徽和垛口。图标源路径与资源均未引入。

扩展盾徽覆盖时，沿用前述 RuleTile 邻接规则与 Tiled 的边界匹配作为设计参考。端点、转角和交汇格依据四邻接识别，直墙在两个此类节点之间均匀补放，使相邻盾徽最多相隔三格。放置结果随已确认布局重算，由现有墙顶材质读取。用横竖直墙、交汇、拆墙和投影识别回归验证。
