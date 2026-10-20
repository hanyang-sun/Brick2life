# 多出生点扩展

2026-10-11 用户要求自主选择多个敌人出生点，并删除旋转/缩放 UI。本扩展替代 requirements-v1.md 中 §3、§4.2 和 SpawnSelection/LevelResult 的单出生点限制；扫描、颜色、共享核心、四方向移动、手动波次与锁定规则继续适用。

- 战斗前，电脑左键点击或手机单指轻点空游戏格添加入口；再点已选格删除。选择按添加顺序排列，编号从 1 开始；删除后重排剩余编号。允许清空选择，至少一个有效入口才能开局。
- 每个新入口必须为空且能到达至少一个核心；无效添加不替换现有地图或选择，界面显示原因。初始无路/占用的默认入口仍允许预览与删除。
- 每个入口独立选择最近可达核心和最短四方向路线；任一已有入口无效时，关卡整体不可开局。
- WavePlan.enemiesPerWave 是所有入口合计。以本局累计出怪数轮流选择入口，跨波继续轮转；入口多于每波敌人数时，后续波继续分配尚未轮到的入口。单局总敌人数少于入口数时，末尾入口可能没有敌人。
- 每个敌人保持出生时所属路线；进入目标核心扣共享生命一次。战斗冻结整组入口/路线；重试保留入口配置，计数归零，从第一个入口开始。
- 相机卡片及 Left/Right/Reset/Lower/Higher/-/+ 按钮删除，对应区域恢复地图输入。现有右键拖动、滚轮、单指拖动、双指捏合和正交投影继续保留。

## 接口

`LevelResult.SpawnRoutes` 是只读的 `SpawnRoute` 集合，每项包含 Spawn、TargetCoreBrickId、只读 Route。Status 为整组可玩性状态。旧 Spawn/TargetCoreBrickId/Route 表示首个入口，仅供单入口兼容，清空时 Spawn 为 (-1,-1)。

`LevelSession.ToggleSpawn` 用于玩家操作；旧 `SelectSpawn` 保留单点替换语义，供原有集成使用。`BattleSession` 冻结全部入口；`EnemyState.SpawnIndex` 指向本局入口列表的零基索引。

验收用例与运行记录见 `.scratch/multiple-spawns/`。
