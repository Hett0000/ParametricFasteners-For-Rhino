# RhinoMM

RhinoMM 是一款面向 FDM 3D 打印的 Rhino 8 Windows 插件。它用于快速放置螺钉通孔、沉孔、沉头孔和螺母槽，并把这些孔保存为可编辑的非破坏式组件。

导出 STEP 或 STL 时，插件在临时副本中自动完成布尔切割；原始 3DM 模型和孔组件保持不变，便于继续调整规格、位置和打印补偿。

> 当前仓库处于产品与技术设计基线阶段，尚未生成可编译插件代码。

## 文档入口

- [产品需求文档](docs/PRODUCT_REQUIREMENTS.md)
- [用户流程](docs/USER_FLOWS.md)
- [技术基础文档](docs/TECHNICAL_FOUNDATION.md)
- [开发准备](docs/DEVELOPMENT.md)
- [产品草图](docs/PRODUCT_SKETCHES.md)
- [决策记录](docs/DECISIONS.md)

## 首版范围

- Rhino 8.20+ Windows，.NET 8，RhinoCommon 与 Eto UI。
- 支持封闭 Brep 和 Extrusion 宿主。
- 支持 M2、M2.5、M3、M4、M5、M6、M8、M10、M12。
- 支持内六角圆柱头、内六角沉头、六角头螺栓和 1 型六角螺母槽。
- 支持曲面/平面放置、起点与轴向放置、贯穿/盲孔、方向翻转。
- 支持全局打印配置和单组件 XY、Z 补偿覆盖。
- 支持非破坏式 STEP/STL 导出与原子失败处理。

首版不包含真实螺纹、Mesh/SubD 宿主、侧向螺母插槽或立即破坏式切割。

## 数据声明

首版规格库计划采用公开的 ISO 等效尺寸，并明确标记为“国标兼容数据，待 GB 原文复核”。在取得合法授权的国标尺寸表并完成逐项校验前，产品不得宣称严格符合国标。
