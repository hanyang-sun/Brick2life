Status: ready-for-agent
Completion: complete

# 形成四类模块清单与清理建议

按 `../spec.md` 完成静态模块、程序集、资源和调用关系梳理，交付 `docs/brick2life-assets-audit-2026-10-15.md` 与可核对清单。当前不执行代码清理。

## Comments

2026-10-15：已读取两份用户文档；当前方向为纯 Unity 仿真。仓库主工程与 task-a 的已提交 Assets 一致；实验记录工作在独立 worktree，尚未合入。

2026-10-15：完成报告与逐文件清单。63 个非 meta 文件分类为直接复用 27、需要修改/适配 9、条件删除候选 3、暂时保留 24；141 个物理文件全部盘点，63 个资源 meta 与 15 个目录 meta 均有目标且 GUID 不重复。三个候选在 Assets 内无外部词法类型/序列化 GUID 引用，历史文档调用仍已标明。`git diff --exit-code -- Assets ProjectSettings Packages` 通过，本轮未改代码或删除资源，未启动 Unity 测试。
