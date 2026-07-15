# 参数化紧固件

“参数化紧固件”是面向 Rhino 8 Windows 与 FDM 3D 打印的螺丝、螺母和补偿孔插件。紧固件本体与按宿主生成的切割模块保存在一个可整体选择的 Rhino 组件组中；导出 STL/STEP 时只对临时副本做布尔，原始 3DM 保持可编辑。

## 已实现功能

- M1.6、M2、M2.5、M3、M4、M5、M6、M8、M10、M12 预设。
- 内六角圆柱头螺钉、内六角沉头螺钉、六角头螺栓和六角螺母。
- 一颗紧固件可同时绑定穿过通孔与螺纹咬合孔，两类宿主分别计算补偿。
- 紧固件与全部切割模块组成一个 Rhino Group，点击任一可见成员均可整体选择。
- 紧固件本体和切割模块分别调节不透明度，并随 3DM 保存。
- 每个被切割体可独立隐藏切割预览；隐藏不会影响 STL/STEP 导出布尔。
- 选择面自动取得位置与法向，或使用起始点与轴向放置。
- 选择既有组件后读取并自动更新；普通模型可转换为参数化紧固件。
- 文档级校验和不修改原模型的 STL/STEP 导出。

## 构建与安装

运行要求为 Rhino 8.18+ Windows；插件以 Rhino 8.18 默认的 .NET 7 为最低运行时，源码可使用 .NET 8 SDK 构建：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./build/build.ps1
```

输出位于 `artifacts/plugin/`。完全退出旧 Rhino 进程后，在 `PluginManager` 中安装 `参数化紧固件.rhp`，运行主命令 `ParametricFasteners` 打开面板。

旧版 `RhinoMMPanel`、`RhinoMMPlaceHole`、`RhinoMMEditHole`、`RhinoMMApplyUpdate`、`RhinoMMAdoptFastener`、`RhinoMMValidate` 和 `RhinoMMExportPrint` 命令继续保留，用于兼容既有工作流。内部 `RhinoMM.*` 元数据键同样保留，以便读取旧 3DM。

如果曾通过 `SetDotNetRuntime` 切换到 `NETFramework`，请改回 `NETCore` 并重启 Rhino。

## 数据声明

当前预设是 ISO 等效工程尺寸，尚未逐项对照有授权的现行 GB/T 原文，因此不能宣称“已通过国标核验”。打印补偿必须按打印机、材料、喷嘴和层高用试片校准。

## 文档

- [产品需求](docs/PRODUCT_REQUIREMENTS.md)
- [用户流程](docs/USER_FLOWS.md)
- [技术基础](docs/TECHNICAL_FOUNDATION.md)
- [每宿主孔配合设计](docs/PER_TARGET_FIT_DESIGN.md)
- [选择、读取与接管设计](docs/SELECTION_AND_ADOPTION_DESIGN.md)
- [开发准备](docs/DEVELOPMENT.md)
- [产品草图](docs/PRODUCT_SKETCHES.md)
- [决策记录](docs/DECISIONS.md)
