# RhinoMM

RhinoMM 是面向 Rhino 8 Windows 与 FDM 3D 打印的参数化紧固件孔插件。它把螺丝代理、每个被切割体的孔配合关系和打印补偿保存为可编辑组件；导出 STL/STEP 时只对临时副本做布尔，原始 3DM 保持可修改。

## 已实现的 MVP

- M1.6、M2、M2.5、M3、M4、M5、M6、M8、M10、M12 预设。
- 内六角圆柱头、沉头、六角头螺栓和六角螺母简化代理体。
- 一颗螺丝可同时绑定两类实体：
  - 穿过实体：标准间隙孔 + FDM 打印修正 + 单绑定修正，最终孔径必须大于公称直径。
  - 咬合实体：公称直径 − 经试片校准的咬合缩减 + FDM 打印修正 + 单绑定修正，最终孔径必须小于公称直径。
- 选择面自动取得位置与法向，或按 Enter 使用起始点与轴向放置。
- 选择代理体或切割体后读取参数；修改后可应用重建全部关联几何。
- 把普通 Brep、Extrusion 或块实例接管为 RhinoMM 参数化螺丝；源对象默认隐藏保留。
- 选择实体并导出 STL/STEP 时，在无界面的临时文档中布尔，不破坏原模型。
- 文档级组件校验。

## 构建

要求 Rhino 8.20+ Windows 与 .NET 8 SDK：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./build/build.ps1
```

输出位于 `artifacts/plugin/`。在 Rhino 中运行 `PluginManager`，安装 `RhinoMM.rhp`；打开面板可运行 `RhinoMMPanel`。

主要命令：`RhinoMMPanel`、`RhinoMMPlaceHole`、`RhinoMMEditHole`、`RhinoMMApplyUpdate`、`RhinoMMAdoptFastener`、`RhinoMMValidate`、`RhinoMMExportPrint`。

## 数据声明

当前预设是 ISO 等效工程尺寸，尚未逐项对照有授权的现行 GB/T 原文，因此不能宣称“已通过国标核验”。打印补偿也必须按打印机、材料、喷嘴和层高用试片校准。

## 文档

- [产品需求](docs/PRODUCT_REQUIREMENTS.md)
- [用户流程](docs/USER_FLOWS.md)
- [技术基础](docs/TECHNICAL_FOUNDATION.md)
- [每宿主孔配合设计](docs/PER_TARGET_FIT_DESIGN.md)
- [选择、读取与接管设计](docs/SELECTION_AND_ADOPTION_DESIGN.md)
- [开发准备](docs/DEVELOPMENT.md)
- [产品草图](docs/PRODUCT_SKETCHES.md)
- [决策记录](docs/DECISIONS.md)
