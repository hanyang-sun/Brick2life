# 洋流航道投影

2026-11-07。在 GitHub 检索 `water shader flow map UV`、`polyline round joins line distance`。范围为当前 main 的地面路线反馈，依据 README、架构及历史开发安排核对现有投影链路。验收覆盖直道、转弯、共用路段、改路、暂停及关投影。

| 候选 | 阅读范围和许可 | 采用方式 |
| --- | --- | --- |
| [MapLibre GL JS](https://github.com/maplibre/maplibre-gl-js) | README、[line_bucket.ts](https://github.com/maplibre/maplibre-gl-js/blob/main/src/data/bucket/line_bucket.ts) 的圆角连接和沿线距离、[BSD-3-Clause 许可](https://github.com/maplibre/maplibre-gl-js/blob/main/LICENSE.txt) | 参考沿线累积距离驱动样式及细分圆角的机制。浏览器矢量地图几何与现有 Unity 投影不匹配，采用自己的短曲线和栅格合并。 |
| [Mochies Unity Shaders](https://github.com/MochiesCode/Mochies-Unity-Shaders) | README、[Water.shader](https://github.com/MochiesCode/Mochies-Unity-Shaders/blob/master/Mochie/Water%20Shader/Water.shader)、[Utilities.cginc](https://github.com/MochiesCode/Mochies-Unity-Shaders/blob/master/Mochie/Common/Utilities.cginc) 中的 FlowUV、[流动贴图文档](https://github.com/MochiesCode/Mochies-Unity-Shaders/wiki/Water-Shader)、[MIT 许可](https://github.com/MochiesCode/Mochies-Unity-Shaders/blob/master/LICENSE) | 参考将纹理流动与几何分离的思路。使用现有 Built-in 管线和无光照着色器，按距终点的长度移动箭头和水纹，无需导入水体、反射、法线纹理或第三方代码。 |

直接消费 `DisplayRoutes`，路径改变时更新一张包含横向偏移和沿线距离的纹理。每处只取最接近的路线，避免共享路段重复混合。内容仍走投影仪相机、投影遮挡与 RGB 补偿。箭头和水纹由同一图案生成，退役旧光带与箭头对象池。使用 Unity 内置 API，桌面离线验证，移动端图形支持待实机验证。

Unity 6000.3.24f1 本地验证通过。成像检查 77 项，连续识别与实时改路检查 1451 项，暂停采集检查 103 项，多入口玩法与回退检查 167 项。另有 1216 项纯 C# 断言通过。成像 fixture 的两条路线共用至少 6 个半格路段，实际投影仪输出保存为 `.scratch/dual-window/ocean-current-20261107/ocean-current-output.png`。该图可见两个旋涡入口、圆角与汇流后的单一航道。
