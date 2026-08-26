# 参数化紧固件 0.35.5：持久安装与重启自修复

## 用户可见变化

- 正式发布提供 Rhino 8 Windows Yak 包和可双击运行的离线安装 ZIP。
- 插件安装到当前 Windows 用户的稳定 Rhino 包目录；下载 ZIP 或解压目录之后可以安全移动或删除。
- 插件在 Rhino 启动时加载，命令、面板和生命周期服务无需再次手动激活。
- 工具栏在 Rhino 首次 Idle 后按固定 RUI GUID 自检：旧路径会被替换，正确路径不会重复加载。
- “关于参数化紧固件”新增 RHP 路径、安装方式、工具栏路径和健康状态。

## 升级方式

1. 关闭 Rhino 8。
2. 完整解压`参数化紧固件-0.35.5-离线安装.zip`。
3. 双击`安装参数化紧固件.cmd`。
4. 启动 Rhino，在“更多 → 关于参数化紧固件”确认“Yak / 安装正常”。

从 0.35.4 手动安装升级不会删除模板、收藏、全局设置或 3DM 中的组件。组件 schema 保持 v17。

## 开发与发布

- `artifacts/plugin`保留为开发调试目录。
- `artifacts/packages`原子生成 Yak、离线 ZIP 与 SHA256。
- 默认不向 McNeel 公共 Package Manager 推送；离线包可直接附加到 GitHub Release。
