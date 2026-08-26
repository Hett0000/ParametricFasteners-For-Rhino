# 开发与部署

## 环境

- Windows 10 或更新版本。
- Rhino 8.18+，插件以 .NET 7 为最低运行时。
- .NET 8 SDK；仓库优先使用 `.dotnet/dotnet.exe`。
- 当前验证环境：Rhino 8.18.25098.11001、RhinoCommon、Eto Forms 2.8.3。

## 构建

完全关闭 Rhino 后运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./build/build.ps1 -Configuration Release
```

脚本依次执行 restore、核心测试、Release 构建、RUI 图标生成、Rhino `Compat.exe` 检查和 Yak 打包。所有步骤先在 `artifacts/.staging/` 完成。

默认部署目标为主 Git 工作区的 `artifacts/plugin/`；即使从 Codex 或其他 Git worktree 构建，也会通过 Git common directory 找到 Rhino 实际使用的主仓库位置。目标存在时，脚本先检查全部文件能否独占打开；被 Rhino 占用时立即停止，原目录不变。验证通过后，旧目录移动到同一主工作区的 `artifacts/backups/<时间>-v<版本>/`，再原子替换为 staging。备份不会自动删除。若该插件 GUID 已在 Rhino 8 注册，脚本同时把 `FileName` 更新到固定的 canonical RHP 路径，避免重启后继续加载旧版本目录。

可通过 `-OutputDirectory artifacts/build-test` 生成独立验证目录，不替换当前安装位置。

最终插件目录仅包含：

- `ParametricFasteners.rhp`
- `ParametricFasteners.rui`
- `RhinoMM.Core.dll`
- `manifest.yml`

`artifacts/plugin/`仅用于开发加载。正式交付位于`artifacts/packages/`：

- `parametric-fasteners-0.38.7-rh8_18-win.yak`
- `参数化紧固件-0.38.7-离线安装.zip`
- `SHA256SUMS.txt`

离线 ZIP 内含一键安装和只读诊断脚本。安装器拒绝在 Rhino 运行时安装，验证 Yak 内容和 SHA256，检测系统级旧注册冲突，并确认稳定用户包目录中的 ASCII RHP。正式注册完全交给 Rhino Package Manager；升级时只清理0.38.3遗留的Yak根项简略路径，用户模板和设置不会被删除。

## 完成定义

1. 核心测试全部通过，Release 构建零警告、零错误。
2. `Compat.exe` 通过 Rhino 8.18 API 检查。
3. Rhino 实际加载后，面板在 280–500 DIP 和 100%–200% 缩放下无横向滚动条。
4. 放置、读取、更新、Undo、透明度、模块开关和 STL/STEP 导出完成实际验证。
5. M1.6 数据、M3 `L+1D`/自定追加深度和多宿主不同孔径具备回归测试。
