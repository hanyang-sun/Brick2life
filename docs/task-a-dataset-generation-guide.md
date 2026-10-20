# 日常识别测试集自动生成

## 使用

Unity 菜单 **Brick2Life → Dataset Generator** 打开“测试集生成”。默认 20 个不同布局、随机种子 `20261020`、五种光照，共 **100 张**。点击“生成测试集”后显示进度，可取消并保留已经完成的样本；关闭窗口不终止后台任务，重新打开可继续查看该编辑器会话中的任务。

工具读取**磁盘上已保存**的 `Assets/Scenes/Brick2LifeSimulation.unity` 及脚本、材质、相机、灯光资源。场景未保存的修改和 Play 模式中的临时布局不作为输入。后台使用独立 Unity 工程运行，当前工作台的布局、灯光、场景及 Play 状态不被生成流程修改。

当前启动器使用 Windows PowerShell 和当前 Unity 编辑器路径。也可在项目根目录运行，保持主工程编辑器打开即可：

```powershell
pwsh -File Tests/GenerateDataset.ps1
# 自定义：12 个布局，仅标准光照，共 12 张；同一种子可重现布局
pwsh -File Tests/GenerateDataset.ps1 -LayoutCount 12 -LightingCount 1 -Seed 42
# 用已安装的其他 Unity 路径运行
pwsh -File Tests/GenerateDataset.ps1 -UnityPath 'D:\Unity\Editor\6000.3.24f1\Editor\Unity.exe'
```

参数 `-Families` 是 `DatasetLayoutFamilies` 位掩码，默认 1023（全部）；`-OutputRoot` 改输出根目录，`-NoWait` 启动后立即返回。每次运行自动创建新目录，已有目录拒绝覆盖。默认脚本会等待并报告完成、取消或失败。首次运行需要导入资源和编译着色器，启动阶段可能比采集本身更慢。

数量允许 1–200 个不同布局，每个布局 1 或 5 张，单次最多 1000 张。默认仅 100 张，没有额外全组合矩阵。若选中的布局类型和调色板不足以产生指定数量的独特布局，明确报错，不以重复布局凑数。

## 布局与光照

布局类型：空板、中心单块、边缘单块、角落单块、横竖线、同色聚簇、棋盘间隔、稀疏随机（3–12 块）、中等随机（25–50 块）、密集随机（75–100 块）。按选中的类型轮流生成，空板只计一个布局；重复内容跳过。颜色来自保存场景中的实际调色板，固定 10×10、单层，每格最多一块。

“五种日常光照”均为双侧白光、开启阴影：

| 条件 | 亮度档位／相对标准强度 | 左侧占比 | 环境光倍数 |
| --- | --- | --- | --- |
| balanced-standard | 标准／1 | 0.5 | 1 |
| balanced-dim | 暗／0.5 | 0.5 | 0.5 |
| balanced-bright | 亮／1.5 | 0.5 | 1.5 |
| left-heavy | 标准／1 | 0.8 | 0.5 |
| right-heavy | 标准／1 | 0.2 | 0.5 |

关闭五种光照时仅生成第一种。表中为 Unity 仿真参数，不是实测照度或真实相机曝光。本版固定相机几何，不额外加入噪声、镜头畸变、投影或实拍样本。

## 输出与交给 B 的内容

默认输出：`SimulationCaptures/Datasets/dataset-时间-随机后缀/`。

```text
request.json             本次配置、隔离工程位置
source-snapshot.json     复制输入的相对路径与 SHA-256
launcher.json            启动状态、后台进程 ID、日志位置
manifest.json            状态、计划／完成数、布局分组及样本清单
frames/
  rgb_....png            实际采集相机 RGB
  rgb_....json           同帧 GT，继续使用现有 schema 3
verification.json        执行 CheckDataset.ps1 后生成
```

GT JSON 仍包含 100 格、帧 ID、棋盘角点、相机 schema 1 和光照 schema 2。清单记录每张图片和标注的路径、SHA-256、布局类型、占用数量、相机／光照配置 ID。图片与 GT 完整写入后才增加清单条目，清单以临时文件替换发布。

**给 B 的识别输入仅为 PNG 与约定公开的棋盘／标定信息。** GT、布局类型、占用数量和清单中的布局分组供组织及评测使用，不作为识别特征。

`layoutGroupId` 根据逐格占用及颜色计算，不包含来源积木 ID、状态版本、相机、光照和时间。同一布局五张图片属于同一组，后续训练／验证／测试划分应整组分配，避免同一布局泄漏。默认 100 张对应 20 个布局，不是 100 个独立布局；它是日常回归集，不构成真实设备准确率证据。

种子、生成器版本 `daily-v1`、调色板顺序相同时，逻辑布局与来源格位 ID 可复现。不同 Unity／GPU／渲染环境不保证 PNG 逐字节相同；帧 ID、时间戳和本地状态版本也不参与布局复现保证。输入文件清单保存采集工程启动前的副本哈希，便于核对源文件变化。

## 进度、取消与失败

清单状态为 `running`、`completed`、`cancelled` 或 `failed`。取消按钮在两次采集之间生效；命令行也可在该运行目录创建 `cancel.request` 文件。取消不会删除已经发布的 PNG/GT。恢复时新开一次运行，不向旧目录续写。

如果 Unity 在编译／许可证／包导入阶段退出，可能尚无 manifest；查看 `launcher.json` 中的 `logPath`。正常采集异常会在 manifest 中写 `failed` 和错误。编辑器服务日志留在 `editor.log`，棋盘／采集组件的错误会使运行失败。强制杀进程或断电可能留下 `running`，此时不能按完成集使用。

生成副本及 Unity 缓存位于 `.scratch/dataset-generation/`，输出与缓存均已 Git 忽略。结束后可按需清理对应运行的缓存目录，保留数据目录即可。

## 检查

```powershell
dotnet run --project Tests/Brick2Life.Checks.csproj
pwsh -File Tests/CheckDataset.ps1 -RunDirectory 'SimulationCaptures/Datasets/具体运行目录'
pwsh -File Tests/CheckDataset.ps1 -RunDirectory 'SimulationCaptures/Datasets/取消的目录' -ExpectedStatus cancelled
pwsh -File Tests/RunSimulationLightingChecks.ps1
```

纯 C# 检查覆盖默认数量、唯一布局、全部类型、种子复现、分组、参数上限及有限布局不足。数据检查核对实际 PNG/GT 数量、哈希、逐格坐标和颜色、分组、光照变体与 schema。Unity 回归额外检查整盘替换后的模型／几何／即时采集一致性、失败不改状态、配对保存拒绝覆盖及失败清理。

编码前比较见 [GitHub 调研](research/dataset-generation-github.md)。

## 本次验证（2026-10-20）

- 纯 C# 共 390 项通过，新增 21 项数据集计划检查。
- 实际生成 `SimulationCaptures/Datasets/dataset-20261020-161427-088-842b8fdd/`：100 张 512×512 PNG + 100 份 GT，20 个唯一布局、10 种布局类型、每组五种光照；配对文件合计约 7.48 MiB。`CheckDataset.ps1` 的 21172 项数据检查通过，并人工查看聚簇／密集布局图像。
- 独立 Unity 回归 `.scratch/lighting-validation/run-20261020-161752-759/`：359 项通过、34 对光照样本；包含新增替换布局、拒绝覆盖和失败回滚检查。
- Windows PowerShell 5.1 菜单启动方式验证通过；中途取消于 3/100 张停止，状态为 cancelled，已完成样本保留。证据位于 `.scratch/dataset-generation/cancel-verification-20261020-1634/`。
- 修正 Windows PowerShell 的 UTF-8 BOM 和空字符串互操作差异，使包清单可被 Unity 解析。冷启动中出现的 Unity Search 索引异常仅留在 editor.log；生成器按实际采集组件异常及配对结果判断失败，完整数据检查通过。
