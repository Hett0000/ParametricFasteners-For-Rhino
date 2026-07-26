# GitHub 提交与新建仓库信息

本文档用于维护 GitHub 仓库资料并发布 `v0.21.0`。

## 1. 新建仓库建议

| 字段 | 建议内容 |
| --- | --- |
| Repository name | `parametric-fasteners` |
| Display name | 参数化紧固件 |
| Owner | 选择个人或组织账号 |
| Visibility | 首次公开发布建议 `Private` 先检查；确认无版权/授权问题后再改为 `Public` |
| Description | Rhino 8 参数化紧固件插件，为 FDM 打印零件创建可编辑螺丝、螺母和补偿孔组件，并支持无损 STEP/STL 导出。 |
| Website | 暂留空；发布文档或演示页完成后再填写 |
| Topics | `rhino3d`, `rhinocommon`, `eto-forms`, `dotnet`, `cad`, `3d-printing`, `fdm`, `fasteners`, `screws`, `stl`, `step` |
| README | 使用仓库现有 `README.md` |
| .gitignore | 使用仓库现有 `.gitignore` |
| License | 当前未包含许可证；公开前必须决定授权方式 |
| Default branch | `main` |

建议不要在 GitHub 创建仓库时自动生成 README、`.gitignore` 或 License，以免和本地仓库冲突。

## 2. 仓库短介绍

参数化紧固件是面向 Rhino 8 Windows 与 FDM 3D 打印的插件。它把常用螺丝、螺母和安装孔抽象为可编辑的 Rhino 组件：用户可以选择标准规格、设置打印补偿、绑定一个或多个宿主实体，并在导出 STEP/STL 或放入 Rhino 时才对临时副本执行布尔，确保原始 3DM 保持可编辑。

## 3. GitHub About 描述

```text
Rhino 8 参数化紧固件插件，为 FDM 打印零件创建可编辑螺丝、螺母和补偿孔组件，并支持无损 STEP/STL 导出。
```

英文备选：

```text
Rhino 8 plugin for editable fastener and compensated hole components for FDM parts, with non-destructive STEP/STL export.
```

## 4. 首次提交信息

```text
Initial release of parametric fastener components
```

中文备选：

```text
初始化参数化紧固件插件项目
```

## 5. 首次 Pull Request 标题与说明

标题：

```text
Add RhinoMM parametric fastener plugin baseline
```

说明：

```markdown
## Summary

- add Rhino 8 parametric fastener plugin source, commands, Eto panel, preset data, and build scripts
- support editable fastener components with control points, per-host cutters, print compensation, statistics, and STEP/STL export
- include product requirements, technical design notes, user flows, release notes, and third-party notices

## Validation

- Build with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./build/build.ps1`
- Verify Rhino 8.18+ loads `参数化紧固件.rhp`
- Run `ParametricFasteners` and check placement, edit, update, refresh, statistics, and export flows
```

## 6. Release `v0.21.0` 建议内容

标题：

```text
v0.21.0 - Automatic host rediscovery after length updates
```

发布说明：

```markdown
## Highlights

- Smart-placed screws persist their host-recognition mode and clearance/thread-engagement process templates.
- Changing shaft length or embed depth rediscovers hosts inside the new effective reach.
- Newly reached deepest hosts receive thread engagement; earlier hosts receive clearance holes, while unreachable bindings are removed.
- Panel, toolbar, batch update, and pre-export apply flows share the same transactional recognition pipeline.
- Rhino, STEP, and STL outputs use the latest rediscovered host bindings.

## Compatibility

- Persistent schema is v8. Existing v7 and classic-placement components retain manual host bindings.
- Existing component IDs, `RhinoMM.*` metadata keys, legacy commands, and existing 3DM files remain compatible.

## Notes

- Preset dimensions are ISO-equivalent engineering data unless explicitly marked otherwise.
- Direct thread-engagement holes are FDM process parameters and must be calibrated for printer, material, nozzle, and layer height.
```

## 7. 推荐 README 开头简介

当前 `README.md` 已可作为仓库首页使用。如果需要更适合 GitHub 首屏的短版介绍，可使用：

```markdown
# 参数化紧固件

参数化紧固件是面向 Rhino 8 Windows 与 FDM 打印的 RhinoCommon 插件。它将螺丝、螺母、通孔、沉孔和咬合预孔保存为可编辑组件，并在导出 STEP/STL 或放入 Rhino 时才对临时副本执行布尔，避免破坏原始 3DM 模型。

核心能力包括 M1.6-M12 常用规格、五类紧固件预设、逐宿主孔配合、打印补偿、控制点编辑、批量更新、统计导出和无损制造文件输出。
```

## 8. 发布前检查清单

- [ ] 确认是否公开仓库；如公开，先复核标准数据、截图、图标和第三方许可。
- [ ] 添加或明确许可证；当前仓库没有 `LICENSE` 文件。
- [ ] 确认 `README.md` 中的构建、安装和命令说明与当前版本一致。
- [ ] 运行 Release 构建脚本并保存结果。
- [ ] 在 Rhino 8.18+ 实机验证 `ParametricFasteners` 面板可打开。
- [ ] 验证放置、读取、应用更新、刷新/清理、Move/Copy/Rotate/Mirror/Paste、统计、放入 Rhino、STEP/STL 导出。
- [ ] 确认 `artifacts/`、本地 Rhino 输出、备份目录和临时文件没有误提交。
- [ ] 确认 `THIRD_PARTY_NOTICES.md` 覆盖图标等第三方资源。
- [ ] 如发布二进制附件，附上 `artifacts/plugin/` 中的 Yak/插件包或压缩包，并说明 Rhino 版本要求。

## 9. 建议仓库文件结构说明

```text
src/RhinoMM.Core/        领域模型、预设、公式、统计、验证
src/RhinoMM.Plugin/      Rhino 命令、Eto 面板、UserData、导出与生命周期服务
tests/RhinoMM.Core.Tests/核心逻辑和回归测试
data/                    紧固件预设数据
docs/                    产品需求、技术设计、用户流程、发布说明
build/                   构建、工具列和图标生成脚本
packaging/               Yak manifest
```

## 10. 重要声明

```markdown
> 当前预设数据是 ISO 等效工程尺寸，尚未逐项对照有授权的现行 GB/T 原文，因此不能宣称“已通过国标核验”。直接咬合预孔不是标准内螺纹，实际保持力依赖材料、层高、打印方向和螺丝，应通过试片校准后使用。
```
