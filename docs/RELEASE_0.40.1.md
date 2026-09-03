# 参数化紧固件 0.40.1 / Parametric Fasteners 0.40.1

## 中文

0.40.1 修复正常放置或更新组件后误报“需重建”的问题。后台健康扫描现在只检查控制点、参数、宿主、对象引用和提交后的派生签名，不再被动重新识别宿主、重新生成 Brep 或运行布尔检查。

- 插件事务期间暂停健康扫描，过期修订结果不会发布。
- 新组件从 Rhino 文档中的最终对象记录几何签名和派生清单。
- 健康、异常及恢复正常都会刷新界面，不再残留旧警告。
- 主面板使用紧凑的“需维护 / 需处理”徽标，不覆盖放置或更新结果。
- 图层、材质、组和可见性差异归入显示提示；预览或布尔关闭归入配置状态。
- 导航器的问题数量只统计需维护、待重绑、损坏和显式布尔失败。

组件 schema 保持 v21，模板 schema 保持 v5，旧 3DM 无需迁移。

## English

Version 0.40.1 fixes false “needs rebuild” reports immediately after normal placement or updates. Passive health scans now verify control points, parameters, hosts, object references, and committed derived metadata without reconciling hosts, regenerating Breps, or running Boolean checks.

- Health scans are suspended during plug-in transactions and stale document revisions are discarded.
- New components record geometry signatures and a derived-object manifest from the final Rhino document objects.
- Healthy recovery reports clear previous warnings reliably.
- A compact maintenance/action-required badge replaces intrusive operation-status warnings.
- Presentation differences and user configuration are no longer counted as component failures.

Component schema remains v21 and template schema remains v5; existing 3DM files require no migration.
