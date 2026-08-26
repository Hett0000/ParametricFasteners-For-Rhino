# 参数化紧固件 0.38.3：Yak 插件发现与注册修复

## 修复内容

- Yak 顶层载荷改为`ParametricFasteners.rhp`和`ParametricFasteners.rui`，中文产品名称和命令保持不变。
- manifest加入插件GUID关键词，构建阶段自动验证顶层RHP数量、RHP/RUI同名、版本、依赖及`rh8_18-win`标签。
- 离线安装器会在当前用户的Rhino插件GUID根项补建`Name`、`FileName`和`LoadMode`，并验证注册目标确实存在。
- 离线包增加`诊断安装.cmd`，可检查Yak列表、稳定安装目录、依赖、下载阻止标记和注册状态。
- 启动日志和“关于参数化紧固件”增加Rhino版本、.NET运行时、安装来源、工具栏状态和命令发现数量。

## 安装说明

推荐完全关闭Rhino后运行离线包中的`安装参数化紧固件.cmd`。也可在 Rhino Package Manager 中选择本地Yak，但安装完成后必须完全关闭并重新启动Rhino，新命令不会在当前安装会话立即出现。

组件schema保持v20，模板schema保持v5，模板库schema保持v2；本次不修改模型数据或紧固件几何。
