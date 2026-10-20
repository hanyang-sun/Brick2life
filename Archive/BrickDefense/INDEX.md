# BrickDefense 历史归档

2026-10-20 按用户确认的范围整体移出活动工程。此目录在 Assets/Packages 外，不参与 Unity 导入、构建或默认 .NET 检查。

- Assets：旧运行时、编辑器、场景、专属资源及算法，配对保留 .meta 和上游许可证。
- Tests：旧检查、入口和 Fixtures，额外保留原 SimulationLayout 与布局检查供历史参考。
- docs：旧需求、发布记录、研究和架构。
- README.md、THIRD_PARTY_NOTICES.md：退役时说明及完整历史声明。
- manifest.json：移动前的 178 个文件及 SHA-256，移动后逐项一致。

根目录 .gitattributes 对本归档禁用换行归一化，使重新克隆后的文件字节仍与校验清单一致。

这是源码资料包，共享字体等实际依赖继续留在主工程。完整恢复退役前 Unity 工程使用 GitHub 上的 b47a6ed，不将整个归档重新放回 Assets。

以后按明确需求核对许可证，将最小算法和必要依赖迁入对应模块。历史规格不自动成为当前项目要求。
