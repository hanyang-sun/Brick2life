# 河豚敌人的投影动画

2026-11-07 在 GitHub 对照了 `AnimatedSprite2D play pause` 和 `Unity AnimatedTile animation start time` 的实现。目标是把已确认的河豚图案接入航道投影，在实际移动、泡泡命中和击败时切换画面，并在暂停时保持动画状态。

| 候选 | 阅读内容与许可 | 取舍 |
| --- | --- | --- |
| Godot | [README](https://github.com/godotengine/godot/blob/master/README.md)、[AnimatedSprite2D 播放与暂停](https://github.com/godotengine/godot/blob/master/scene/2d/animated_sprite_2d.cpp#L465)、[MIT 许可](https://github.com/godotengine/godot/blob/master/LICENSE.txt) | 参考循环动画和单次反应的时序区分。C++ 节点依赖 Godot，无法直接接入现有 Unity 投影。 |
| Unity 2D Extras | [README](https://github.com/Unity-Technologies/2d-extras/blob/master/README.md)、[AnimatedTile 的相位与帧序列](https://github.com/Unity-Technologies/2d-extras/blob/master/Runtime/Tiles/AnimatedTile/AnimatedTile.cs#L70)、[Unity Companion License](https://github.com/Unity-Technologies/2d-extras/blob/master/LICENSE.md) | 参考相位错开。Tilemap 帧列表适合静态格位，河豚沿连续坐标运动，因此复用现有投影片和材质属性。 |

两者仅供思路对照。游动相位由战斗里程驱动，受击与击败由已有的命中事件驱动，局部变化用已经试投的程序图案呈现。项目无需外部源码、贴图或包，适合当前离线渲染链路。用 Unity 批处理核对朝向、投影命中、击败消散、暂停与重置。
