# 固定格位识别与变化确认：GitHub 检索

检索日期：2026-10-23。范围：任务 B 的单帧 10×10 占用/颜色识别，以及连续帧上的增加、删除、移动确认。验收是只根据 Unity 顶置相机 RGB 得到与真值可比的状态矩阵，并在增删移动后输出带格位、颜色、事件类型和时间戳的事件。不读取 Ground Truth、编辑器操作或仿真实例 ID。

依据 `docs/architecture.md`、`docs/brick2life-development-plan.md` 和 `docs/task-a-camera-metadata-guide.md`。旧 `PlainBackgroundRecognizer` 只作对照，不整段迁入。

## 关键词

`lego-warp sched-read fixed grid color`、`homography grid cell sampling`、`tangram cell color Lab`、`moku annotations_to_grid`。

## 候选比较

| 候选 | 实际阅读 | 授权 | 匹配与决定 |
| --- | --- | --- | --- |
| [wasv/lego-warp](https://github.com/wasv/lego-warp) | 2026-10-23 读完 README。`sched-read` 的 C++ 源文件抓取被环境拦截，没有读到实现，因此不复制。 | 仓库元数据为 MIT；未读到许可证正文，不把代码引进仓库。 | 先校正平面，再按固定格读取颜色。本项目相机和棋盘几何已知，不检测绿色底板，也不引入 OpenCV。只保留“格内取色、不靠整图连通域猜原点”。 |
| [kaya-go/moku](https://github.com/kaya-go/moku/blob/76131c7e/src/moku/grid.py) `76131c7e` | 读了 `grid.py` 中 `_compute_homography`、`_apply_homography`、`annotations_to_grid` 的完整函数，以及仓库 AGPL-3.0 许可证正文。 | AGPL-3.0。不复制、不改编源码。 | 四点直接线性变换把棋盘角映到单位平面，再把检测框中心量化到格。它消费的是检测框，不是 RGB 分类；AGPL 也不适合引入。底板单应不能代表高 1 的积木顶面。 |
| [alf-labs/tangram analyzer](https://github.com/alf-labs/tangram/blob/main/analyzer/README.md) | 读了 README 中的定位、Lab 通道和格心采样说明。 | README 写明 MIT。未复制 Python。 | 格位确定后只采样格心，避免边缘混色。六边形轮廓和 OpenCV 不适用于本场景。 |
| 本仓库 `Archive/BrickDefense/Assets/Scripts/Vision/PlainBackgroundRecognizer.cs` | 2026-10-23 通读该文件。 | 项目既有代码。 | 调色板距离和“不确定不判成空”可以沿用思路。相对最左/最上原点、连通域拆块、恰好三种颜色、空棋盘拒绝，都不能用于固定棋盘。不复制 `NormalizedLab.cs`。 |

## 采用的做法

思路参考，不引入依赖，不复制上述源码。

1. 像素位置使用任务 A 已记录的约定：`WorldToViewportPoint`，`x = viewportX * imageWidth - 0.5`，`y = (1 - viewportY) * imageHeight - 0.5`。纯 C# 回放用同一约定解释 `projectionMatrix * worldToCameraMatrix`。
2. 每个格子在世界高度 `y = 1`（单位立方体顶面）的中心区域取 3×3 样本。不用底板 `y = 0` 的单应，避免透视把顶面投影到邻格。
3. 用色相角和饱和度对调色板分类。白光把颜色拉亮或压暗时色相保持不变；低饱和度视为底板或网格线。色相过远或两种颜色太接近则为未知，不记为空。
4. 连续帧必须整盘一致才更新确认状态。第一次稳定结果只建立基线，不把全盘记成增加。之后同色且唯一的一减一增记为移动；同色有多组候选时只记增加和删除，并标成不确定，与 `BoardDelta` 不伪造实例身份的规则一致。
5. 真值比较放在识别之后的评分里，识别函数不接收快照。
6. 结果面板继续用工程里已有的 uGUI（Text、Button、LayoutGroup）。不另引界面包。面板画在屏幕上，不进入顶置相机；格子等宽，开关只改显示。

## 验证

- 2026-10-23 `dotnet run --project Tests/Brick2Life.Checks.csproj`：识别相关 29 项通过，并包含原有布局、合同、确认会话和数据集检查。覆盖投影方向、四角与相邻同色、亮度缩放、未知颜色、单帧抖动、增加、删除、移动和同色歧义。
- Unity 6000.3.24f1 隔离工程实际渲染 TopCamera：26 项通过，单帧和确认状态均为 100/100。播放中增加绿色、删除、把红色从 (0,0) 移到 (1,0)，再在角格 (0,0) 增加黄色，事件与格位一致。投影矩阵和 `WorldToViewportPoint` 的像素差小于 0.001。结果在 `.scratch/vision-recognition/run-20261023-191143-903/verification.json`。这次没有验收真实相机、遮挡或投影干扰。
