# Windows 实拍照片选择器

日期：2026-10-12。用户报告 v1.3.0 桌面版点击“导入照片”没有反应。实现前完成 GitHub 检索。

## 范围及验收

Windows x64 非开发 Player 点击“导入照片”直接打开系统选图窗口；支持 PNG/JPG/JPEG 与中文路径；取消不替换现有照片或地图，界面明确反馈取消；选图/解码错误显示可读提示。Unity 编辑器保留原入口，Android 不引用 Windows API。本次随修复交付 v1.3.1 本地包。

检索词：`Unity GetOpenFileName OPENFILENAME file dialog`、`UnityStandaloneFileBrowser StandaloneFileBrowserWindows.cs license`。

## 比较

| 候选 | 实际阅读源码与许可 | 匹配与成本 | 决定 |
| --- | --- | --- | --- |
| [gkngkc/UnityStandaloneFileBrowser](https://github.com/gkngkc/UnityStandaloneFileBrowser) | README、`Assets/StandaloneFileBrowser/StandaloneFileBrowser.cs`、`StandaloneFileBrowserWindows.cs`、`LICENSE.txt`：MIT，2017 Gökhan Gökçe | 运行时原生选图，窗口 owner、扩展名过滤、取消返回空结果。Windows 实现依赖 `System.Windows.Forms` 与 Ookii.Dialogs，README 注明旧 API compatibility 要求；本项目是 Unity 6，Mono 当前可用但不应额外引入整套旧依赖 | 采用“原生模态窗口绑定 Unity 窗口、过滤图片、取消返回空结果”的机制，针对 Windows 做小型 API 适配；不复制其源码或插件 |
| [yasirkula/UnitySimpleFileBrowser](https://github.com/yasirkula/UnitySimpleFileBrowser) | README、`Plugins/SimpleFileBrowser/Scripts/FileBrowser.cs` 中 `ShowLoadDialog`、`OnOperationSuccessful`、`OnOperationCanceled`、`SetFilters`，`LICENSE.txt`：MIT | 可跨平台浏览本地文件及区分取消/成功，有 Android 支持；依赖 uGUI 预制体、资源及完整文件管理器，当前项目使用 IMGUI，Windows 用户期望系统文件窗口 | 借鉴成功/取消分离和保留原状态；当前 Windows 修复不引入包。Android 相册入口另行验收 |
| [cdanek Windows File Selector gist](https://gist.github.com/cdanek/0f78a83f4626e2596b1af5ef86b5b30e) | 阅读 gist 页面；未确认独立授权 | 表明可用 CommonDialog/GetOpenFileName 与 Unity 衔接 | 不采用其代码，Win32 ABI 以微软原始文档为准 |

## 接口适配

- `PhotoFileSource.PickPath` 在 Windows Player 选择平台适配器，在 Editor 保持原有委托。其余平台继续现有路径入口。
- Infrastructure 新增 Windows 专用适配器，使用 Windows 自带 `GetOpenFileNameW` 和 `OPENFILENAMEW`。以 Unicode 处理文件名，将窗口绑定当前 Unity 主线程窗口，限制存在的 PNG/JPG/JPEG，设置不改变当前工作目录。单选，取消返回空，API 异常抛给 UI。
- Presentation 的 Import 捕获选图异常，取消显示反馈；隐藏 Windows 中不再需要的空白路径输入框。
- 既有照片字节读取、尺寸校验、分析、颜色标定和关卡状态更新保持原接口。

没有复制上游源码或二进制，属于有限机制参考及平台接口适配，无需追加新第三方依赖。

## 主要 API 依据

- [GetOpenFileNameW](https://learn.microsoft.com/en-us/windows/win32/api/commdlg/nf-commdlg-getopenfilenamew)：成功与取消/失败返回值。
- [OPENFILENAMEW](https://learn.microsoft.com/en-us/windows/win32/api/commdlg/ns-commdlg-openfilenamew)：Unicode 双零终止过滤器、文件缓冲、owner、FILEMUSTEXIST/PATHMUSTEXIST/NOCHANGEDIR。

## 验证

用真实 Windows Player 回归“点击→出现原生窗口→取消→再次打开→选择实拍 JPG→显示照片”，并检查中文/空格路径、PNG 和 JPEG。不会以 EditorUtility 的编辑器测试替代 Player 验证。
