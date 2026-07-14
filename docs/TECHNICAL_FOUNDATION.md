# 技术基础文档

## 1. 技术基线

- **运行环境**：Rhino 8.18+ Windows。
- **目标框架**：以 `.NET 7` 为最低目标，`AnyCPU`；兼容 Rhino 8.20+ 的 .NET 8 运行时。
- **插件 SDK**：RhinoCommon；界面使用 Eto Forms。
- **插件类型**：General Utility `.rhp`，最终通过 Yak 分发。
- **当前工作机状态**：已检测到 Rhino 8.18.25098.11001，并使用本地 .NET 8 SDK 完成构建。

Rhino 8.18 默认使用 .NET 7；Rhino 8.20 起默认使用 .NET 8。插件针对 .NET 7 编译，以覆盖当前工作机，同时避免新的 `.NET Framework` 依赖。参考 [RhinoCommon 指南](https://developer.rhino3d.com/guides/rhinocommon/)和[迁移到 .NET Core](https://developer.rhino3d.com/en/guides/rhinocommon/moving-to-dotnet-core/)。

## 2. 建议解决方案结构

```text
src/
  RhinoMM.Core/          预设、参数、补偿公式、验证规则
  RhinoMM.Geometry/      RhinoCommon 切割体与布尔服务
  RhinoMM.Plugin/        命令、Eto 面板、文档事件、UserData、导出
tests/
  RhinoMM.Core.Tests/    无 Rhino UI 的快速测试
  RhinoMM.Integration/   Rhino 环境中的几何、持久化和导出测试
data/
  fastener-presets.v1.json
docs/
```

`Core` 不依赖 UI；`Plugin` 只负责编排输入、文档状态和服务调用。几何构造集中在 `Geometry`，避免命令类重复实现尺寸逻辑。

## 3. 公共命令

| 命令 | 用途 |
| --- | --- |
| `RhinoMMPanel` | 打开或聚焦停靠面板 |
| `RhinoMMPlaceHole` | 通过命令行选项放置组件 |
| `RhinoMMEditHole` | 编辑现有组件 |
| `RhinoMMAdoptFastener` | 将当前普通螺丝对象接管为 RhinoMM 组件 |
| `RhinoMMRelinkHole` | 重新绑定宿主 |
| `RhinoMMExportPrint` | 无损导出 STEP/STL |
| `RhinoMMValidate` | 检查预设、组件和宿主状态 |

命令英文名保持稳定，面板和命令提示使用中文资源。脚本化命令不得依赖面板当前焦点。

## 4. 领域模型

### 4.1 FastenerPreset

```text
schemaVersion       数据格式版本
presetId            稳定唯一 ID，例如 gb-compatible.socket-cap
displayNameZh       中文显示名
standardReference   标准题录和年份
equivalentSource    ISO 等效来源与版本
dataStatus          iso-equivalent | gb-verified
kind                socketCap | countersunk | hexBolt | hexNut
sizes[]             各公称规格及尺寸
```

每条 `size` 至少包含公称直径、粗牙螺距、三档通孔直径、头部/螺母外形尺寸和深度。规格覆盖从 M1.6 开始；如果某类标准来源没有对应 M1.6 尺寸，则构建时报告缺项，不得由相邻规格插值。沉头角度必须来自数据文件，不得散落硬编码在 UI 或命令中。

### 4.2 PrintProfile

```text
profileId
name
xySizeDeltaMm
zDepthDeltaMm
directBiteReductionBySize
createdAt
updatedAt
```

`xySizeDeltaMm` 表示打印机孔径建模修正；正值扩大 CAD 孔以补偿 FDM 孔偏小。`directBiteReductionBySize` 按公称规格保存直接咬合缩量，因为同一个绝对值不适合同时用于 M1.6 和 M12。全局配置保存在插件设置中，孔组件保存配置名称和数值快照，数值快照是几何计算的最终依据。

### 4.3 HoleComponentData

使用带独立 GUID 的自定义 Rhino `UserData`，并通过 `ArchivableDictionary` 序列化：

```text
schemaVersion
componentId
presetId
presetDataVersion
nominalSize
fitClass
proxyObjectId
adoptedSourceObjectId?
profileSnapshot
xyOverrideMm?
zOverrideMm?
depthMode
blindDepthMm?
placementPlane
axisFlip
rotationRadians
targetBindings[]
state              valid | brokenLink | invalidGeometry
```

必须实现复制、变换、读写和版本迁移。未知的更高版本数据只能只读显示，不能以旧代码覆盖。参考 Rhino 官方 [User Data 示例](https://developer.rhino3d.com/en/samples/rhinocommon/user-data/)。

### 4.4 HoleTargetBinding

```text
targetObjectId
shaftFitRole              clearance | threadEngagement
fitClass?                 close | normal | loose，仅通孔使用
directBiteReductionMm?    正数，仅咬合预孔使用
xyCorrectionOverrideMm?
depthMode                 throughTarget | blind
blindDepthMm?
includeHeadSeat           每个组件最多一个 true
```

同一组件内 `targetObjectId` 必须唯一。首版 `threadEngagement` 表示直接拧入打印材料的无螺纹圆柱预孔，不等同于标准 6H 内螺纹；数据模型为未来的 `tapPilot` 或真实螺纹策略预留版本字段，但首版不暴露未实现模式。

### 4.5 FastenerProxy

`FastenerProxy` 是组件在 Rhino 视口中的可选择代表，使用标准公称尺寸生成简化头部和杆部，不生成螺旋牙型。它与 `HoleComponentData` 共享稳定 `componentId`，并满足：

- 选择代理、任一切割体或对象属性中的组件链接，都能解析到同一个组件。
- 代理变换更新 `placementPlane`，随后重建全部绑定切割体并重新验证相交。
- 代理默认不参与打印导出，只作为放置、选择和装配检查的可视对象。
- 更新几何时优先使用 `ObjectTable.Replace` 保持 `proxyObjectId`；若 Rhino 必须产生新 ID，则在同一事务中修正所有反向索引。

### 4.6 组件索引

维护文档级运行时索引：`componentId -> proxyObjectId/cutterObjectIds/targetBindings`，以及 `RhinoObjectId -> componentId` 反向索引。索引只用于选择与性能优化，3DM 中的 UserData 才是持久化事实来源；打开文档、Undo/Redo 或索引不一致时必须从 UserData 重建。

## 5. 几何规则

- 所有预设尺寸以毫米存储，构造前转换为当前 Rhino 文档单位。
- 打印机 XY 修正加到最终建模直径或六角对边尺寸一次；Z 增量加到最终槽深一次。
- 穿过通孔的最终建模直径：`D_clearance = D_standard(fitClass) + C_printer + C_binding`。
- 咬合预孔的最终建模直径：`D_engagement = D_nominal - I_bite + C_printer + C_binding`，其中 `I_bite > 0` 来自按规格校准的数据或显式输入。
- 必须验证 `D_clearance > D_nominal + tolerance`，以及 `0 < D_engagement < D_nominal - tolerance`。
- 切割体必须是闭合、方向一致的 Brep；生成后立即验证 `IsSolid`、有效性和包围盒。
- 圆柱头由通孔柱体和头部沉孔组成；沉头由通孔和数据驱动角度的锥台组成。
- 六角头和螺母槽使用以对边尺寸定义的六角柱，允许绕轴旋转。
- 贯穿长度取所有绑定宿主在孔轴方向上的投影范围，并在两端增加 `max(10 × absoluteTolerance, 1 mm)` 安全余量。
- 盲孔从入口沿确认后的向内轴线延伸，最终深度必须大于文档绝对容差。
- 同一孔组件按 `HoleTargetBinding` 为每个宿主生成独立轴孔切割体；头部沉孔/沉头只并入 `includeHeadSeat = true` 的宿主切割体。
- M1.6 等小尺寸在布尔前比较最小切割尺寸与文档绝对容差：容差大于最小尺寸的 `1/50` 时警告，大于 `1/20` 时阻止计算。

## 6. 放置和绑定

### 面放置

1. 通过子对象选择获取 `BrepFace` 和宿主对象。
2. 将用户点击投影到面上，求 UV、点和法向，建立局部放置平面。
3. 在法向两侧用文档容差偏移测试实体包含关系；能判断时自动指向内部，不能判断时要求用户确认。
4. 持续重建轻量预览，只有确认时才写入文档。

### 起点放置

1. 先收集明确的宿主，并为每个 GUID 创建独立 `HoleTargetBinding`。
2. 获取起点和第二点，规范化轴向；两点距离小于容差则拒绝。
3. 使用当前视图/CPlane 仅辅助输入，不把它作为隐式最终方向。

### 变换和生命周期

- 监听 Rhino 文档对象变换、替换、删除和撤销相关事件。
- 同一变换中只移动宿主时，对其唯一关联或同变换多宿主的组件应用相同变换。
- 多个宿主发生不同变换、宿主替换为不支持类型或 GUID 消失时，将组件标记为 `brokenLink`。
- 事件处理必须防止递归，并把自动同步纳入同一 Undo 记录。

### 选择读取与编辑同步

- 监听 Rhino 选择变化，但用短延迟合并连续事件，避免框选时反复刷新面板。
- 选中一个 RhinoMM 代理或切割体时，通过反向索引读取 `HoleComponentData`，面板切换为编辑状态并显示组件 ID、预设版本和绑定列表。
- 选中宿主时只列出关联组件；存在多个关联项时必须由用户明确选择。
- 面板编辑写入独立草稿模型；输入变化只更新预览，不立即覆盖 UserData。
- 用户点击“应用”后执行 `Validate -> Build proxy -> Build per-target cutters -> BeginUndoRecord -> Replace geometry and UserData -> Rebuild index -> Redraw`。
- 构建或验证在写入前失败时不触碰文档；写入阶段异常时撤销整个记录，旧组件继续有效。

### 普通模型接管

1. 接受单个闭合 Brep、Extrusion 或 InstanceReference；若已有 RhinoMM UserData，直接转入编辑。
2. 从圆柱面、旋转对称候选、包围盒和头部轮廓估算轴线、杆径及头型，只产生候选，不直接提交。
3. 将候选尺寸与预设表做容差范围匹配；显示候选标准、规格、偏差和“自动候选/手动选择”状态。
4. 用户确认预设、规格、入口和轴向后，生成简化代理与切割预览。
5. 应用时默认隐藏源对象、保存 `adoptedSourceObjectId` 并创建 RhinoMM 组件；块实例不修改共享定义。
6. 源对象被删除或无法恢复不影响已接管组件，但面板显示“源备份不可用”。

## 7. 导出管线

1. **收集**：仅接受用户选择的闭合 Brep/Extrusion，并查找显式绑定组件及每个宿主的孔配合角色；排除 `RhinoMM::Fasteners` 代理、`RhinoMM::Cutters` 和隐藏的接管源对象。
2. **预检**：验证单位、对象类型、闭合状态、组件版本、绑定唯一性、头部承座唯一性、最终孔径、链接与相交。
3. **复制**：创建临时 Headless `RhinoDoc`，复制宿主并转换 Extrusion 为 Brep。
4. **重建**：为每个宿主从其 `HoleTargetBinding` 生成专属切割体，不依赖文档中可能过期的显示 Brep。
5. **布尔**：只把该宿主的专属切割体用于该宿主副本，按宿主执行 `Brep.CreateBooleanDifference`，使用源文档绝对容差和实体方向。
6. **诊断**：批量布尔失败时在内存中逐组件重试，仅用于定位；最终仍执行全有或全无策略。
7. **写出**：STEP 使用 `FileStp.Write`；STL 使用 `FileStl.Write` 和显式网格参数。包含 M1.6 时，网格最大边长不得大于最小有效孔径的 `1/20`。
8. **提交**：先写同目录临时文件，成功关闭并复读验证后原子替换目标路径。
9. **清理**：关闭临时文档，不修改源文档选择之外的状态。

RhinoCommon 提供官方的 [STEP 写出接口](https://developer.rhino3d.com/api/rhinocommon/rhino.fileio.filestp)、[STL 写出接口](https://developer.rhino3d.com/api/rhinocommon/rhino.fileio.filestl)和 [Brep 布尔差集](https://developer.rhino3d.com/api/rhinocommon/rhino.geometry.brep/createbooleandifference?overload=1)。

## 8. 容差、单位与错误处理

- 未设置模型单位时禁止生成与导出。
- 几何运算始终使用 `RhinoDoc.ModelAbsoluteTolerance`；数据比较使用毫米空间的独立小容差。
- STL 默认将临时几何缩放为毫米，界面明确显示输出单位。
- 所有用户错误返回结构化诊断：错误代码、组件 ID、宿主 ID、阶段、可执行修复建议。
- 不在日志中写入用户完整文件路径以外的敏感数据；不联网读取规格数据。

## 9. 测试策略

- **数据测试**：架构、唯一 ID、M1.6–M12 覆盖、粗牙螺距、正数尺寸、三档孔径顺序、来源字段。
- **公式测试**：单位换算、通孔正余量、咬合缩量、打印机修正、六角对边、深度和安全余量。
- **几何测试**：四类孔型的 M1.6/M2/M6/M12、平面/曲面、翻转、盲孔/贯穿。
- **多宿主测试**：同一轴线的通孔宿主与咬合宿主得到不同孔径；切割体不跨宿主误用；承座只出现在指定宿主。
- **文档测试**：UserData 保存重开、复制、变换、删除、Undo/Redo 和版本迁移。
- **读取编辑测试**：分别选择代理、切割体和宿主；验证面板读取结果、歧义列表、原子更新、ID 保持、失败回滚和 Undo/Redo。
- **接管测试**：普通 Brep、Extrusion、块实例、已有 RhinoMM 对象、无法识别对象、源对象隐藏/恢复和手动映射。
- **导出测试**：STEP/STL 复读、闭合性、单位、几何体积、切割体排除、源对象不变。
- **失败测试**：无交集、非闭合、尺寸退化、布尔失败、目标文件已存在和临时写入失败。

集成验收应比较导出前后源对象 GUID、几何哈希和组件计数，并使用已知量规几何验证孔径，而不仅检查命令返回成功。

## 10. 标准数据治理

标准题录以国家标准信息公共服务平台为准。当前首版关注：

- [GB/T 70.1-2008 内六角圆柱头螺钉](https://openstd.samr.gov.cn/bzgk/std/newGbInfo?hcno=AA481BA038B2F9CE68879E6B087F4F73)
- [GB/T 70.3-2023 降低承载能力内六角沉头螺钉](https://openstd.samr.gov.cn/bzgk/std/newGbInfo?hcno=F6B723A6B05D3C27FD72C3D925A956A4)
- GB/T 5783-2025 紧固件 六角头螺栓 全螺纹
- [GB/T 6170-2015 1 型六角螺母](https://openstd.samr.gov.cn/bzgk/std/newGbInfo?hcno=BDDE5AF77AC2FBC2D194289F10C69A4B)
- [GB/T 5277-1985 紧固件 螺栓和螺钉通孔](https://openstd.samr.gov.cn/bzgk/std/newGbInfo?hcno=7C6B6039F8E5FB5FD8F318E06BAFB0B5)
- [GB/T 152.3-1988 紧固件 圆柱头用沉孔](https://openstd.samr.gov.cn/bzgk/gb/newGbInfo?hcno=9959F71ECC47D1E24E0FF3B19CF53CE8)
- [GB/T 196-2025 普通螺纹 基本尺寸](https://openstd.samr.gov.cn/bzgk/std/newGbInfo?hcno=7923264EF5B418EA786AAEACA4835708)
- [GB/T 197-2018 普通螺纹 公差](https://openstd.samr.gov.cn/bzgk/std/newGbInfo?hcno=4B9C1010476FF8385A59AD3076EC3A68)

采标全文或尺寸表没有合法来源时，只能录入可追溯的 ISO 等效数据并设置 `dataStatus: iso-equivalent`。取得授权资料、双人复核并通过尺寸回归测试后，才允许切换为 `gb-verified`。

ISO 273 的一般用途通孔范围包含从 1 mm 起的细、中、粗系列；螺纹基础尺寸和公差分别由普通公制螺纹标准管理。咬合预孔属于 FDM 工艺参数，不应伪装成 GB/ISO 标准尺寸。
