# Third-party notices

## Noto Sans SC 中文界面字体

- 来源：[notofonts/noto-cjk](https://github.com/notofonts/noto-cjk)，树版本 `f8d157532fbfaeda587e826d4cd5b21a49186f7c` 的 `Sans/SubsetOTF/SC/NotoSansSC-Regular.otf`；内部字体版本 2.004。
- 本地资源：`Assets/Resources/Fonts/NotoSansSC-Regular.otf`，未修改字体二进制、名称或字形。字体 metadata 版权为 © 2014-2021 Adobe (http://www.adobe.com/).
- SIL Open Font License 1.1 完整文本见 `Assets/ThirdParty/NotoSansSC/OFL.txt`，播放器随包许可证为 `Assets/Resources/Fonts/NotoSansSC-OFL.txt`；来源与 SHA-256 见同目录 SOURCE.md。
- 用于 Brick2Life 仿真工作台与光照面板的中文文字。3d-group/unity-simple-localization 仅作为集中错误文案查找机制参考，未采用其源码或包。检索及具体整合见 `Archive/BrickDefense/docs/research/chinese-ui-github.md`。

## Unity UI（uGUI）2.0.0

- 来源：Unity 6000.3.24f1 随附的官方内置包 `com.unity.ugui`，在 `Packages/manifest.json` 中声明版本 2.0.0。
- 上游源码：https://github.com/Unity-Technologies/uGUI 。本项目通过 Package Manager 使用原版包，没有复制或修改包源码。
- 授权：Unity Companion License for Unity-dependent projects；Copyright © 2015-2020 Unity Technologies ApS。完整许可证入口和声明随该包的 `LICENSE.md` 保留。
- 使用范围：仿真场景的 Canvas、RawImage、比例布局、工作台文字与鼠标格位提示的四条 Image 边线，显示 TopCameraRGB；研究比较见 `docs/research/simulation-camera-preview-github.md`、`docs/research/simulation-workspace-view-github.md` 与 `docs/research/simulation-grid-pointer-github.md`。Cinemachine 和 Sunny Valley Studio 网格系统仅作机制比较，没有引入其源码或包。

旧算法及完整授权随源码保存在 Archive/BrickDefense/THIRD_PARTY_NOTICES.md 和对应历史目录。
