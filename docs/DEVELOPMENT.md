# 开发准备

## 1. 当前状态

此仓库目前只包含产品、技术和视觉设计基线。当前机器检查结果：

- 未检测到 Rhino 安装。
- `dotnet --info` 未发现 .NET SDK。
- 可见多个 .NET Runtime，但 Runtime 不能替代 SDK 完成编译。

因此下一阶段首先是安装并验证开发环境，不应在缺少 Rhino 的情况下声称插件已构建或测试。

## 2. 必需环境

- Windows 10 或更新版本。
- Rhino 8.18 或更新的 Rhino 8 服务版本。
- .NET 8 SDK x64。
- Visual Studio 2022（`.NET desktop development`）或 VS Code＋C# Dev Kit。
- Rhino Visual Studio Extension 或 `Rhino.Templates`。
- Git。

安装参考：[Rhino Windows 开发工具指南](https://developer.rhino3d.com/guides/rhinocommon/installing-tools-windows/)。

## 3. 环境验证

安装完成后记录以下输出：

```powershell
dotnet --info
dotnet --list-sdks
Get-Item 'C:\Program Files\Rhino 8\System\RhinoCommon.dll'
```

最低通过条件：存在 8.x SDK、RhinoCommon 文件可读，并能启动 Rhino 8 的 .NET Core 7 或更新运行时。

## 4. 计划中的构建命令

项目骨架建立后统一使用：

```powershell
dotnet restore
dotnet build -c Debug
dotnet test -c Debug
dotnet build -c Release
```

Yak 打包命令和清单将在插件骨架生成后写入仓库脚本；在清单、版本、目标 Rhino 版本和许可证尚未确认前不发布包。

## 5. 分支与提交约定

- 默认分支：`main`。
- 功能分支：`feature/<short-name>`。
- 修复分支：`fix/<short-name>`。
- 提交信息采用简短祈使句，可使用 `docs:`、`feat:`、`fix:`、`test:`、`build:` 前缀。
- 不提交 `bin/`、`obj/`、`.rhp`、`.yak`、IDE 用户设置、临时模型或私有标准全文。

## 6. 完成定义

任何功能只有同时满足以下条件才算完成：

1. 产品需求中的对应编号已实现。
2. 核心公式具备单元测试，Rhino 行为具备集成测试。
3. 错误路径和 Undo/Redo 已验证。
4. 中文界面无截断，高 DPI 下可用。
5. 导出结果复读通过，源文档不变。
6. 文档和决策记录同步更新。

小尺寸功能提交还必须包含 M1.6 的数据校验、容差阈值和自适应 STL 网格测试；多宿主功能提交必须验证同一组件可对不同宿主输出不同轴孔直径。

选择与编辑功能提交必须覆盖组件索引重建、代理/切割体双入口读取、原子更新回滚和 Undo/Redo；接管功能提交必须证明源对象默认可恢复，且块定义不会被修改。
