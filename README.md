# 参数化紧固件

参数化紧固件是面向 Rhino 8 Windows 与 FDM 3D 打印的 RhinoCommon 插件。它把螺丝、螺母、通孔、沉孔和咬合预孔保存为可编辑组件，并在“放入 Rhino”、STEP 或 STL 导出时才对宿主副本执行布尔，避免破坏原始 3DM 模型。

当前版本：`0.21.0`

## 主要功能

- 支持 M1.6、M2、M2.5、M3、M4、M5、M6、M8、M10、M12 常用规格。
- 支持内六角圆柱头螺钉、内六角沉头螺钉、六角头螺栓、六角螺母和自定义尺寸热熔螺母。
- 六角螺母使用带打印补偿的六角盲槽；热熔螺母使用可设置长度、外径、孔径补偿和深度补偿的 45° 导角安装孔，新建时默认增加 1 mm 切割深度。
- 六角螺母和热熔螺母单击封闭宿主面即可完成位置、方向、宿主绑定和创建。
- 通过控制点读取、更新、批量更新和删除紧固件组件。
- 一颗螺丝可绑定多个宿主，并区分“穿过通孔”和“螺纹咬合孔”。
- 孔径修正、通孔配合、咬合缩减、预览开关和导出布尔开关可按组件或放置预设管理。
- 咬合孔支持贯穿宿主、螺杆长度 + 1D、螺杆长度 + 2D 和自定义盲孔深度。
- 支持螺丝头嵌入深度，提供 0 和齐平快捷设置。
- 默认使用智能所见即所得放置：虚拟紧固件跟随鼠标，实时预览本体、切割体和宿主角色；无捕捉点时落在鼠标所在面，有端点、中点、圆心、交点、节点或 Point 时优先精确捕捉。
- 智能放置沿螺杆轴线自动识别穿过体与咬合体：单宿主默认为咬合体，多宿主时最深处为咬合体、其余为穿过体；经典手动绑定流程继续保留。
- 新建智能组件在修改螺杆长度后会重新扫描有效轴向范围，自动新增或移除宿主，并重新划分穿过体与咬合体；经典放置及旧组件保持手工绑定。
- 支持整体移动、复制、打组移动/复制、Gumball Alt 复制、Ctrl+C/V、跨文档粘贴和 3DM 导入后的组件修复。
- 支持放入 Rhino、导出 STEP、导出 STL、紧固件统计和 Excel 清单。
- “放入 Rhino”图标左击只输出布尔宿主，右击同时输出关联紧固件实体；钢制紧固件与热熔螺母自动使用共享的拉丝钢或黄铜 PBR 材质，并放入独立渲染图层。
- 两个透明度滑块统一控制当前文档全部组件，并作为后续新建组件的跨会话默认值。
- 全部参数化组件复用文档级本体/切割显示材质；复制、粘贴和导入不会再为每颗副本新增材质或贴图资源。
- 热熔螺母界面使用紧凑孔径/深度摘要，完整计算公式通过悬停提示查看，窄面板不再出现横向滚动条。
- 面板和紧固件统计窗口自动跟随 Rhino 浅色/深色主题，并在 Rhino 运行期间切换主题时即时更新。
- 深色模式使用分层卡片、高对比状态色和独立浅色线性图标，输入框、下拉框和滚动条继续采用 Rhino 原生样式。
- 保留旧 `RhinoMM*` 命令和 `RhinoMM.*` 元数据键，兼容已有模型。

## 安装

1. 构建或获取插件输出目录 `artifacts/plugin/`。
2. 在 Rhino 8 中打开 `PluginManager`。
3. 安装 `artifacts/plugin/参数化紧固件.rhp`。
4. 如需工具列，在同一目录加载 `参数化紧固件.rui`。
5. 运行命令 `ParametricFasteners` 打开面板。

如果 Rhino 曾通过 `SetDotNetRuntime` 切换到 `NETFramework`，请改回 `NETCore` 并重启 Rhino。

## 常用命令

| 命令 | 作用 |
| --- | --- |
| `ParametricFasteners` | 打开参数化紧固件面板 |
| `ParametricFastenersPlace` | 启动虚拟紧固件跟随鼠标的智能连续放置 |
| `ParametricFastenersPlaceClassic` | 使用经典的手动宿主选择和面/点放置 |
| `ParametricFastenersApplyUpdate` | 将面板参数应用到选中的控制点 |
| `ParametricFastenersExportToRhino` | 生成布尔后的普通 Rhino 宿主副本 |
| `ParametricFastenersExportToRhinoWithFasteners` | 生成布尔宿主及关联紧固件渲染实体 |
| `ParametricFastenersExport` | 导出 STEP/STL |
| `ParametricFastenersStatistics` | 统计当前选择或全部紧固件 |
| `ParametricFastenersValidate` | 校验当前文档中的组件数据 |

旧命令 `RhinoMMPanel`、`RhinoMMPlaceHole`、`RhinoMMEditHole`、`RhinoMMApplyUpdate`、`RhinoMMAdoptFastener`、`RhinoMMValidate` 和 `RhinoMMExportPrint` 仍作为兼容入口保留。

## 构建

要求：

- Windows
- Rhino 8.18 或更新版本
- .NET SDK，仓库通过 `global.json` 固定 SDK 版本

构建命令：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./build/build.ps1 -Configuration Release
```

构建脚本会执行核心测试、Release 构建、工具列生成、兼容性检查和原位部署。部署前会把旧插件目录备份到 `artifacts/backups/<timestamp>-<version>/`。如果 Rhino 正在占用插件文件，脚本会停止并提示关闭 Rhino，不会覆盖当前插件。

## 仓库结构

```text
src/RhinoMM.Core/         领域模型、尺寸计算、孔深计算、统计和 Excel 写入
src/RhinoMM.Plugin/       Rhino 命令、Eto 面板、几何、持久化、生命周期和导出服务
tests/RhinoMM.Core.Tests/ 核心逻辑和插件源码回归测试
data/                     紧固件预设数据
docs/                     产品文档、技术文档、用户流程、发布说明
build/                    构建、工具列和图标资源生成脚本
packaging/                Yak manifest
artifacts/                本地构建产物，默认不入库
```


## 文档

- [产品需求](docs/PRODUCT_REQUIREMENTS.md)
- [用户流程](docs/USER_FLOWS.md)
- [技术基础](docs/TECHNICAL_FOUNDATION.md)
- [每宿主孔配合设计](docs/PER_TARGET_FIT_DESIGN.md)
- [选择、读取与接管设计](docs/SELECTION_AND_ADOPTION_DESIGN.md)
- [开发说明](docs/DEVELOPMENT.md)
- [产品草图](docs/PRODUCT_SKETCHES.md)
- [决策记录](docs/DECISIONS.md)
- [GitHub 提交说明](docs/GITHUB_SUBMISSION.md)

## 数据与精度说明

当前预设是 ISO 等效工程尺寸，尚未逐项对照有授权的现行 GB/T 原文，因此不能宣称“已通过国标核验”。直接咬合预孔不是标准内螺纹，实际保持力依赖材料、层高、打印方向、螺丝和打印机校准，应通过试片验证后用于生产。

## 第三方资源

图标体系基于 Lucide 图标语义并按 Rhino 工具列尺寸重新导出。第三方说明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
