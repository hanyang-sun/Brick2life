# 仿真灯光安排：机器视觉照明与 Unity 可实现范围

检索日期：2026-10-19。场景背景：Unity 6000.3.24f1、Built-in、10×10 彩色单层立方体、顶部 Perspective 摄像机。目前单个 Directional Light、Intensity=1、无阴影作为既有功能对照。本轮仅研究资料，未修改代码、场景或灯光参数。

检索关键词：`machine vision lighting dome ring bar three dimensional shadow glossy`、`brightfield illumination spot bar ring dome`、`Unity 6000.3 Built-in Area runtime baked soft shadows`。下列来源均实际打开阅读，以厂商应用指南和 Unity 6.3 官方手册为依据。

## 已核实的布光事实

| 实物照明方案 | 已核实的特点和限制 | 对本项目的意义（建议） |
| --- | --- | --- |
| 顶部漫射／大面积柔光 | 延展光源的漫射照明可减轻高光、改善均匀性；厂商将漫射、同轴或环形照明列为减少三维凸起物阴影的选择。[Edmund Optics：Common Illumination Types](https://www.edmundoptics.com/knowledge-center/application-notes/illumination/choose-the-correct-illumination/) | 适合作为以后真实彩色识别装置的稳定基准；“更真实”不要求把基准故意做成强阴影。Unity 中应明确采用何种近似。 |
| 单侧／双侧条形灯 | Basler 说明多条灯可从不同侧组合照明；角度可调整。[Basler：Light in Machine Vision](https://www.baslerweb.com/en-us/learning/illumination/) 条形和 Spot 侧向亮场可突出结构，反光物上也可能产生热点；条形的宽形状便于照亮大对象，却不会自动消除反光。[Edmund Optics：Spot and Bar Lights](https://www.edmundoptics.com/knowledge-center/application-notes/illumination/brightfield-illumination-with-spot-and-bar-lights-for-machine-vision/) | 双侧可作为简洁、可控的起点。比较单侧和双侧对底板阴影与顶面颜色的影响，不预先宣称某强度比最好。 |
| 镜头周围环形灯 | 环形亮场可提供较均匀、少阴影照明，但反光物会出现圆形高光；工作距离和视野也影响效果。[Edmund Optics：Ring Lights](https://www.edmundoptics.com/knowledge-center/application-notes/illumination/brightfield-illumination-with-ring-lights-for-machine-vision/) | 可以列为对照，不应将“环形”理解为绝对无反光。小环形灯的效果不能直接外推到整块 10×10 板面。 |
| 穹顶／平顶漫射照明 | 多方向漫射照明适合复杂、光亮表面；会改变细小纹理对比度，装置开口与相机视野需要协调。[Edmund Optics：Dome Lights](https://www.edmundoptics.com/knowledge-center/application-notes/illumination/brightfield-illumination-with-dome-lights-for-machine-vision/) Basler 同样说明穹顶提供多入射方向的间接漫射光。[Basler：Dome Light](https://www.baslerweb.com/en-us/illumination/dome-light/?light_category_parent=Dome+Light) | 可作为理想稳定照明方向，但本项目没有真实穹顶测量依据，也不宜先在 Built-in 中搭复杂光学装置。 |

这些是照明几何与厂商样例，不能当作本项目的实测结果。上述 Edmund Spot/Bar、Ring、Dome 比较使用的设备清单包含单色相机；它们支持阴影/反射几何的判断，不直接证明本项目 RGB 三色分类准确率。真实彩色效果还要对实际积木、光谱与相机进行采样验证。

## Unity Built-in 的明确限制

- Directional Light 是无限远、平行入射且没有距离衰减的模型。因此移动其位置不能模拟近处灯管的位置变化，也不能把它等同于有长度的有限光源。[Unity 6.3：Types of Light component](https://docs.unity3d.com/6000.3/Documentation/Manual/Lighting.html)
- 同一官方页面的 Area Lights 说明 Built-in Area 只能烘焙到 lightmap，运行时不可用。当前积木会增删和移动，烘焙 Area 不适合直接作为动态积木实时受光与遮挡的主方案；画出发光矩形也不等于已经拥有实时面积照明。
- Built-in 的 Soft Shadows 有助于柔化阴影贴图边缘；Baked Shadow Angle/Radius 仅适用于相应 Baked/Mixed 配置。给 Directional 或 Spot 勾选 Soft Shadows，不会把原来光源的几何改成真实连续灯管／矩形面光源。[Unity 6.3：Built-in Light Inspector](https://docs.unity3d.com/6000.3/Documentation/Manual/class-Light.html)
- 实时阴影有额外渲染成本，软阴影通常比硬阴影增加 GPU 开销；bias、normal bias 和阴影贴图分辨率也可能引入假自阴影或阴影脱离，评测时应区分这些渲染瑕疵和真实物理遮挡。[Unity 6.3：Shadow troubleshooting](https://docs.unity3d.com/6000.3/Documentation/Manual/ShadowPerformance.html)

## 已有官方 Unity 仿真示例

- [Unity Perception：Phase 2 Custom Randomizations](https://github.com/Unity-Technologies/com.unity.perception/blob/main/com.unity.perception/Documentation~/Tutorial/Phase2.md) 提供双 Directional 不同强度的教程，可看作主补光组织参考：复制光源，绕 Y 分别旋转约 +60° / −60°，分别限制强度范围（0.5～3 与 0～0.4），逐迭代随机强度和颜色，各灯保留自己的强度上下限。可借鉴“独立主补参数＋按实验迭代采样”的组织方式。教程基于 HDRP，示例随机范围不是本项目最佳参数；Directional 的位置仍不控制照度。该包 [README](https://github.com/Unity-Technologies/com.unity.perception) 已声明停止官方维护，本轮不安装，仅作机制参考。
- [Unity-Technologies/VolumetricLighting：README](https://github.com/Unity-Technologies/VolumetricLighting) 包含 Adam 演示的 Area 和 Tube Light，但仅声明测试 Unity 5.6～2018.3，且光源仅支持 Deferred；Tube 没有完整物体阴影，只提供两个 ShadowPlanes，Area 阴影也是明确标注的粗略近似。它说明“仿真灯管”有现成研究实现，却不适合作为当前 Unity 6.3 项目的直接安装答案；现代兼容性、动态遮挡和性能均未验证。

以上仅阅读官方教程与 README，不构成引入源码所需的完整许可/实现审计。

## 推荐的第一阶段（本项目建议，尚未实施）

先做“固定白色双侧宽角 Spot＋固定弱环境光”的低成本近似，保持 Built-in 与动态编辑。灯放在板面上方的相对两侧，朝向棋盘中心，覆盖底板和单位积木顶面；方位、高度、角度、范围要通过整板采样调整。优先从对称等强开始，再把不对称强弱作为实验变量。若先用一盏主光加较弱补光，也应记录而非宣称其比例最优。

这样选择的理由是可控制位置、方向、距离变化和遮挡，便于形成可重复的单侧／双侧对照。双 Spot 仍然是两个有限点源的锥形照明近似，不是物理精确的双条灯，也不是穹顶漫射。需要更接近长灯管时，可在后续比较少量离散采样灯的近似，但应评估性能、多重阴影和反光形状，不能仅增加发光外观模型就宣称完成模拟。

保留原有无阴影 Directional 作为回归对照。基准档先固定相机、材质和环境光，再开启实时阴影，检查材质确实响应光照。不要同时改镜头、曝光、颜色材质和布光，否则无法判断识别变化来自哪一项。对于彩色分类，基准使用固定白光；任意 RGB 彩灯可作为特殊干扰条件，不能替代真实白光色偏实验。

## 后续实验与记录

建议按同一布局采集以下配对数据：原始无阴影基准、单侧灯、双侧灯、随后可选的多方位近似。先让灰色／白色参考区域的整体亮度大致匹配，再比较板面中心/四角均匀性、积木附近阴影、高光和饱和区域，避免把“多开一盏灯更亮”误判成布局更好。记录采集图像、GT、识别结果，并分别报告占用/颜色准确率与几何落点误差。

后续一次改变一个因素：灯强、单侧比例、灯方位、材质光泽、环境光；再进行组合测试。曝光变化作为独立图像形成变量，投影开启/关闭作为独立干扰变量。反馈进入相机后，应重新测识别表现，不能只验投影关闭时的图像。允许的亮度、角度和识别门槛需由本项目试验确定，本记录不虚构最优参数。

每次实验至少保存灯类型、位置/方向、颜色、强度、范围、Spot 角、阴影设置、环境光、材质和图像颜色空间条件；相机配置 ID 只覆盖已记录的相机配置，不能代替整套照明实验身份。纯 Unity 阴影和材质模型下的成功仅说明该合成条件的效果，不能证明真实灯管、实际塑料反射、传感器响应或投影设备已经可靠。

本轮没有引入外部包，也没有复制第三方代码；仅据第一方资料提出布光与验收建议。后续若正式实现功能，再按项目 `AGENTS.md` 的 GitHub 候选与有限借鉴规则完成对应编码前记录。
