# 参数化紧固件 0.38.4：Yak 首次启动激活修复

## 修复内容

- 移除离线安装器对Rhino插件根项`Name/FileName/LoadMode`的手工写入，避免Yak与简略注册并存造成首次启动只登记、不加载。
- 从0.38.3升级时，只清理明确指向参数化紧固件Yak目录的临时根项`FileName`；完整`PlugIn`注册、命令清单和用户设置保持不变。
- 修复安装诊断在`FileName`为空时的PowerShell异常，并同时检查`PlugIn\FileName`、关键命令、禁用状态、依赖和.NET运行时。
- 开发部署改为优先读取完整`PlugIn`子项，避免本机构建覆盖稳定Yak注册。

## 安装说明

推荐关闭Rhino后运行离线包中的`安装参数化紧固件.cmd`。也可在Rhino Package Manager中选择本地Yak。两种方式都只需在安装后完整启动Rhino一次；当前安装会话不会立即出现新命令。

组件schema保持v20，模板schema保持v5，模板库schema保持v2；本版本不修改几何、组件数据或用户设置。
